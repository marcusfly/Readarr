using System;
using System.Net;
using System.Net.Http;
using NLog;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Http
{
    public interface ICachedHttpResponseService
    {
        HttpResponse Get(HttpRequest request, bool useCache, TimeSpan ttl);
        HttpResponse<T> Get<T>(HttpRequest request, bool useCache, TimeSpan ttl)
            where T : new();
    }

    public class CachedHttpResponseService : ICachedHttpResponseService
    {
        private readonly ICachedHttpResponseRepository _repo;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public CachedHttpResponseService(ICachedHttpResponseRepository httpResponseRepository,
                                         IHttpClient httpClient,
                                         Logger logger)
        {
            _repo = httpResponseRepository;
            _httpClient = httpClient;
            _logger = logger;
        }

        public HttpResponse Get(HttpRequest request, bool useCache, TimeSpan ttl)
        {
            var cached = _repo.FindByUrl(request.Url.ToString());
            var now = DateTime.UtcNow;

            if (useCache && IsUsable(cached) && cached.Expiry > now)
            {
                _logger.Trace($"Returning cached response for [GET] {request.Url}");
                return ToResponse(request, cached);
            }

            try
            {
                var result = _httpClient.Get(request);

                if (IsCacheable(result))
                {
                    if (cached == null)
                    {
                        cached = new CachedHttpResponse
                        {
                            Url = request.Url.ToString(),
                        };
                    }

                    cached.LastRefresh = now;
                    cached.Expiry = now.Add(ttl);
                    cached.Value = result.Content;
                    cached.StatusCode = (int)result.StatusCode;

                    _repo.Upsert(cached);
                }
                else if (useCache && IsStaleFallbackAvailable(cached, now) && IsTransientFailure(result))
                {
                    return GetStaleResponse(request, cached, $"HTTP {(int)result.StatusCode}");
                }

                return result;
            }
            catch (HttpException ex) when (useCache && IsStaleFallbackAvailable(cached, now) && IsTransientFailure(ex.Response))
            {
                return GetStaleResponse(request, cached, $"HTTP {(int)ex.Response.StatusCode}");
            }
            catch (WebException ex) when (useCache && IsStaleFallbackAvailable(cached, now) && IsTransientFailure(ex))
            {
                return GetStaleResponse(request, cached, ex.Status.ToString());
            }
            catch (HttpRequestException ex) when (useCache && IsStaleFallbackAvailable(cached, now))
            {
                return GetStaleResponse(request, cached, ex.GetType().Name);
            }
        }

        public HttpResponse<T> Get<T>(HttpRequest request, bool useCache, TimeSpan ttl)
            where T : new()
        {
            var response = Get(request, useCache, ttl);
            return new HttpResponse<T>(response);
        }

        private static bool IsCacheable(HttpResponse response)
        {
            var statusCode = (int)response.StatusCode;

            return statusCode >= 200 &&
                   statusCode < 300 &&
                   response.ResponseData != null &&
                   response.ResponseData.Length > 0 &&
                   !string.IsNullOrWhiteSpace(response.Content);
        }

        private static bool IsUsable(CachedHttpResponse cached)
        {
            return cached != null &&
                   cached.StatusCode >= 200 &&
                   cached.StatusCode < 300 &&
                   !string.IsNullOrWhiteSpace(cached.Value);
        }

        private static bool IsStaleFallbackAvailable(CachedHttpResponse cached, DateTime now)
        {
            return IsUsable(cached) &&
                   cached.Expiry <= now &&
                   cached.LastRefresh >= now.AddDays(-CachedHttpResponse.StaleRetentionDays);
        }

        private static bool IsTransientFailure(HttpResponse response)
        {
            if (response == null)
            {
                return false;
            }

            var statusCode = (int)response.StatusCode;

            return response.StatusCode == HttpStatusCode.RequestTimeout ||
                   statusCode == 429 ||
                   response.HasHttpServerError ||
                   (!IsCacheable(response) && statusCode >= 200 && statusCode < 300);
        }

        private static bool IsTransientFailure(WebException exception)
        {
            return exception.Status == WebExceptionStatus.ConnectFailure ||
                   exception.Status == WebExceptionStatus.ConnectionClosed ||
                   exception.Status == WebExceptionStatus.KeepAliveFailure ||
                   exception.Status == WebExceptionStatus.NameResolutionFailure ||
                   exception.Status == WebExceptionStatus.ProxyNameResolutionFailure ||
                   exception.Status == WebExceptionStatus.ReceiveFailure ||
                   exception.Status == WebExceptionStatus.SendFailure ||
                   exception.Status == WebExceptionStatus.Timeout;
        }

        private HttpResponse GetStaleResponse(HttpRequest request, CachedHttpResponse cached, string failure)
        {
            _logger.Warn($"Returning stale cached response for [GET] {request.Url} after transient provider failure: {failure}");
            return ToResponse(request, cached);
        }

        private static HttpResponse ToResponse(HttpRequest request, CachedHttpResponse cached)
        {
            return new HttpResponse(request, new HttpHeader(), cached.Value, (HttpStatusCode)cached.StatusCode);
        }
    }
}
