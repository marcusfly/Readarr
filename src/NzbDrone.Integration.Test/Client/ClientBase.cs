using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using NLog;
using NzbDrone.Common.Serializer;
using Readarr.Http;
using Readarr.Http.REST;
using RestSharp;

namespace NzbDrone.Integration.Test.Client
{
    public class ClientBase
    {
        private const string JsonContentType = "application/json";

        protected readonly RestClient _restClient;
        protected readonly string _resource;
        protected readonly string _apiKey;
        protected readonly Logger _logger;
        private readonly bool _assertDisableCache;

        public ClientBase(RestClient restClient, string apiKey, string resource)
            : this(restClient, apiKey, resource, true)
        {
        }

        protected ClientBase(RestClient restClient, string apiKey, string resource, bool assertDisableCache)
        {
            _restClient = restClient;
            _resource = resource;
            _apiKey = apiKey;
            _assertDisableCache = assertDisableCache;

            _logger = LogManager.GetLogger("REST");
        }

        public RestRequest BuildRequest(string command = "")
        {
            var request = new RestRequest(_resource + "/" + command.Trim('/'));

            request.AddHeader("X-Api-Key", _apiKey);

            return request;
        }

        public string Execute(RestRequest request, HttpStatusCode statusCode)
        {
            LogRequest(request);

            var response = _restClient.Execute(request);
            _logger.Info("Response: {0}", response.Content);

            if (response.ErrorException != null)
            {
                throw response.ErrorException;
            }

            response.ErrorMessage.Should().BeNullOrWhiteSpace();

            response.StatusCode.Should().Be(statusCode, response.Content ?? string.Empty);

            return response.Content;
        }

        public T Execute<T>(RestRequest request, HttpStatusCode statusCode)
            where T : class, new()
        {
            var content = Execute(request, statusCode);

            return Json.Deserialize<T>(content);
        }

        internal T ExecuteJson<T>(RestRequest request, string body, HttpStatusCode statusCode)
            where T : class, new()
        {
            LogRequest(request, body);

            using var httpClient = new HttpClient();
            using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method.ToString().ToUpperInvariant()), _restClient.BuildUri(request));
            httpRequest.Headers.Add("X-Api-Key", _apiKey);
            httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(JsonContentType));

            if (!string.IsNullOrWhiteSpace(body))
            {
                httpRequest.Content = new StringContent(body, Encoding.UTF8, JsonContentType);
            }

            using var response = httpClient.SendAsync(httpRequest).GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            _logger.Info("Response: {0}", content);

            if (!response.IsSuccessStatusCode && response.StatusCode != statusCode)
            {
                response.StatusCode.Should().Be(statusCode, content ?? string.Empty);
            }
            else
            {
                response.StatusCode.Should().Be(statusCode, content ?? string.Empty);
            }

            return string.IsNullOrWhiteSpace(content) ? default : Json.Deserialize<T>(content);
        }

        private void LogRequest(RestRequest request, string body = null)
        {
            _logger.Info("{0}: {1}", request.Method, _restClient.BuildUri(request));
            foreach (var parameter in request.Parameters)
            {
                _logger.Info("Request parameter {0} ({1}): {2}", parameter.Name, parameter.Type, parameter.Value);
            }

            if (!string.IsNullOrWhiteSpace(body))
            {
                _logger.Info("Request body: {0}", body);
            }
        }

        private static void AssertDisableCache(RestResponse response)
        {
            var headers = response.Headers;
            ((string)headers.SingleOrDefault(c => c.Name == "Cache-Control")?.Value ?? string.Empty).Split(',').Select(x => x.Trim())
                .Should().BeEquivalentTo("no-store, no-cache".Split(',').Select(x => x.Trim()));
            headers.Single(c => c.Name == "Pragma").Value.Should().Be("no-cache");
            headers.Single(c => c.Name == "Expires").Value.Should().Be("-1");
        }
    }

    public class ClientBase<TResource> : ClientBase
        where TResource : RestResource, new()
    {
        public ClientBase(RestClient restClient, string apiKey, string resource = null)
            : base(restClient, apiKey, resource ?? new TResource().ResourceName)
        {
        }

        public ClientBase(RestClient restClient, string apiKey, string resource, bool assertDisableCache)
            : base(restClient, apiKey, resource ?? new TResource().ResourceName, assertDisableCache)
        {
        }

        public List<TResource> All()
        {
            var request = BuildRequest();
            return Get<List<TResource>>(request);
        }

        public PagingResource<TResource> GetPaged(int pageNumber, int pageSize, string sortKey, string sortDir, string filterKey = null, object filterValue = null)
        {
            var request = BuildRequest();
            request.AddQueryParameter("page", pageNumber.ToString());
            request.AddQueryParameter("pageSize", pageSize.ToString());
            request.AddQueryParameter("sortKey", sortKey);
            request.AddQueryParameter("sortDir", sortDir);

            if (filterKey != null && filterValue != null)
            {
                request.AddQueryParameter(filterKey, filterValue.ToString());
            }

            return Get<PagingResource<TResource>>(request);
        }

        public TResource Post(TResource body, HttpStatusCode statusCode = HttpStatusCode.Created)
        {
            var request = BuildRequest();
            request.Method = Method.Post;
            return ExecuteJson<TResource>(request, body.ToJson(), statusCode);
        }

        public TResource Put(TResource body, HttpStatusCode statusCode = HttpStatusCode.Accepted)
        {
            var request = BuildRequest();
            request.Method = Method.Put;
            return ExecuteJson<TResource>(request, body.ToJson(), statusCode);
        }

        public TResource Get(int id, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var request = BuildRequest(id.ToString());
            return Get<TResource>(request, statusCode);
        }

        public TResource GetSingle(HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var request = BuildRequest();
            return Get<TResource>(request, statusCode);
        }

        public void Delete(int id)
        {
            var request = BuildRequest(id.ToString());
            Delete(request);
        }

        public object InvalidGet(int id, HttpStatusCode statusCode = HttpStatusCode.NotFound)
        {
            var request = BuildRequest(id.ToString());
            return Get<object>(request, statusCode);
        }

        public object InvalidPost(TResource body, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        {
            var request = BuildRequest();
            request.Method = Method.Post;
            return ExecuteJson<object>(request, body.ToJson(), statusCode);
        }

        public object InvalidPut(TResource body, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        {
            var request = BuildRequest();
            request.Method = Method.Put;
            return ExecuteJson<object>(request, body.ToJson(), statusCode);
        }

        public T Get<T>(RestRequest request, HttpStatusCode statusCode = HttpStatusCode.OK)
            where T : class, new()
        {
            request.Method = Method.Get;
            return Execute<T>(request, statusCode);
        }

        public T Post<T>(RestRequest request, HttpStatusCode statusCode = HttpStatusCode.Created)
            where T : class, new()
        {
            request.Method = Method.Post;
            return Execute<T>(request, statusCode);
        }

        public T Put<T>(RestRequest request, HttpStatusCode statusCode = HttpStatusCode.Accepted)
            where T : class, new()
        {
            request.Method = Method.Put;
            return Execute<T>(request, statusCode);
        }

        public void Delete(RestRequest request, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            request.Method = Method.Delete;
            Execute<object>(request, statusCode);
        }
    }
}
