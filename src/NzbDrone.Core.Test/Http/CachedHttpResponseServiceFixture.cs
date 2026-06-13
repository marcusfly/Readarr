using System;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Http;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Http
{
    [TestFixture]
    public class CachedHttpResponseServiceFixture : CoreTest<CachedHttpResponseService>
    {
        private const string Url = "https://metadata.example.test/author/1";
        private HttpRequest _request;

        [SetUp]
        public void SetUp()
        {
            _request = new HttpRequest(Url);
        }

        [Test]
        public void should_return_fresh_cached_response_without_calling_provider()
        {
            GivenCachedResponse(DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));

            var result = Subject.Get(_request, true, TimeSpan.FromHours(1));

            result.Content.Should().Be("{\"name\":\"cached\"}");
            Mocker.GetMock<IHttpClient>().Verify(v => v.Get(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public void should_return_stale_response_for_transient_http_failure()
        {
            GivenCachedResponse(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMinutes(-5));
            GivenProviderResponse(HttpStatusCode.ServiceUnavailable, "temporarily unavailable");

            var result = Subject.Get(_request, true, TimeSpan.FromHours(1));

            result.Content.Should().Be("{\"name\":\"cached\"}");
            result.StatusCode.Should().Be(HttpStatusCode.OK);
            Mocker.GetMock<ICachedHttpResponseRepository>().Verify(v => v.Upsert(It.IsAny<CachedHttpResponse>()), Times.Never());
        }

        [Test]
        public void should_return_stale_response_for_transient_http_exception()
        {
            GivenCachedResponse(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMinutes(-5));
            var response = BuildResponse(HttpStatusCode.TooManyRequests, "rate limited");
            Mocker.GetMock<IHttpClient>()
                  .Setup(v => v.Get(_request))
                  .Throws(new TooManyRequestsException(_request, response));

            var result = Subject.Get(_request, true, TimeSpan.FromHours(1));

            result.Content.Should().Be("{\"name\":\"cached\"}");
        }

        [Test]
        public void should_return_stale_response_for_network_timeout()
        {
            GivenCachedResponse(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMinutes(-5));
            Mocker.GetMock<IHttpClient>()
                  .Setup(v => v.Get(_request))
                  .Throws(new WebException("timed out", WebExceptionStatus.Timeout));

            var result = Subject.Get(_request, true, TimeSpan.FromHours(1));

            result.Content.Should().Be("{\"name\":\"cached\"}");
        }

        [Test]
        public void should_return_stale_response_for_transport_failure()
        {
            GivenCachedResponse(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMinutes(-5));
            Mocker.GetMock<IHttpClient>()
                  .Setup(v => v.Get(_request))
                  .Throws(new HttpRequestException("connection failed"));

            var result = Subject.Get(_request, true, TimeSpan.FromHours(1));

            result.Content.Should().Be("{\"name\":\"cached\"}");
        }

        [Test]
        public void should_not_return_stale_response_for_permanent_http_failure()
        {
            GivenCachedResponse(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMinutes(-5));
            var response = BuildResponse(HttpStatusCode.NotFound, "not found");
            Mocker.GetMock<IHttpClient>()
                  .Setup(v => v.Get(_request))
                  .Throws(new HttpException(_request, response));

            Assert.Throws<HttpException>(() => Subject.Get(_request, true, TimeSpan.FromHours(1)));
        }

        [Test]
        public void should_not_return_stale_response_after_retention_window()
        {
            GivenCachedResponse(DateTime.UtcNow.AddDays(-CachedHttpResponse.StaleRetentionDays - 1), DateTime.UtcNow.AddDays(-1));
            var response = BuildResponse(HttpStatusCode.ServiceUnavailable, "temporarily unavailable");
            Mocker.GetMock<IHttpClient>()
                  .Setup(v => v.Get(_request))
                  .Throws(new HttpException(_request, response));

            Assert.Throws<HttpException>(() => Subject.Get(_request, true, TimeSpan.FromHours(1)));
        }

        [Test]
        public void should_not_cache_empty_successful_response()
        {
            GivenProviderResponse(HttpStatusCode.OK, string.Empty);

            var result = Subject.Get(_request, true, TimeSpan.FromHours(1));

            result.Content.Should().BeEmpty();
            Mocker.GetMock<ICachedHttpResponseRepository>().Verify(v => v.Upsert(It.IsAny<CachedHttpResponse>()), Times.Never());
        }

        [Test]
        public void should_not_cache_failed_response()
        {
            GivenProviderResponse(HttpStatusCode.ServiceUnavailable, "temporarily unavailable");

            var result = Subject.Get(_request, true, TimeSpan.FromHours(1));

            result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            Mocker.GetMock<ICachedHttpResponseRepository>().Verify(v => v.Upsert(It.IsAny<CachedHttpResponse>()), Times.Never());
        }

        [Test]
        public void should_preserve_stale_response_when_provider_returns_empty_success()
        {
            GivenCachedResponse(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMinutes(-5));
            GivenProviderResponse(HttpStatusCode.OK, string.Empty);

            var result = Subject.Get(_request, true, TimeSpan.FromHours(1));

            result.Content.Should().Be("{\"name\":\"cached\"}");
            Mocker.GetMock<ICachedHttpResponseRepository>().Verify(v => v.Upsert(It.IsAny<CachedHttpResponse>()), Times.Never());
        }

        [Test]
        public void should_cache_non_empty_successful_response()
        {
            GivenProviderResponse(HttpStatusCode.OK, "{\"name\":\"fresh\"}");

            var result = Subject.Get(_request, true, TimeSpan.FromHours(1));

            result.Content.Should().Be("{\"name\":\"fresh\"}");
            Mocker.GetMock<ICachedHttpResponseRepository>()
                  .Verify(v => v.Upsert(It.Is<CachedHttpResponse>(c =>
                      c.Url == Url &&
                      c.Value == "{\"name\":\"fresh\"}" &&
                      c.StatusCode == (int)HttpStatusCode.OK)));
        }

        private void GivenCachedResponse(DateTime lastRefresh, DateTime expiry)
        {
            Mocker.GetMock<ICachedHttpResponseRepository>()
                  .Setup(v => v.FindByUrl(Url))
                  .Returns(new CachedHttpResponse
                  {
                      Id = 1,
                      Url = Url,
                      LastRefresh = lastRefresh,
                      Expiry = expiry,
                      Value = "{\"name\":\"cached\"}",
                      StatusCode = (int)HttpStatusCode.OK,
                  });
        }

        private void GivenProviderResponse(HttpStatusCode statusCode, string content)
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(v => v.Get(_request))
                  .Returns(BuildResponse(statusCode, content));
        }

        private HttpResponse BuildResponse(HttpStatusCode statusCode, string content)
        {
            return new HttpResponse(_request, new HttpHeader(), content, statusCode);
        }
    }
}
