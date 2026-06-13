using NzbDrone.Common.Http;

namespace NzbDrone.Common.Cloud
{
    public interface IReadarrCloudRequestBuilder
    {
        IHttpRequestBuilderFactory Services { get; }
        IHttpRequestBuilderFactory Metadata { get; }
        IHttpRequestBuilderFactory MetadataOpenLibrary { get; }
        IHttpRequestBuilderFactory MetadataRreadingGlasses { get; }
    }

    public class ReadarrCloudRequestBuilder : IReadarrCloudRequestBuilder
    {
        public ReadarrCloudRequestBuilder()
        {
            //TODO: Create Update Endpoint
            Services = new HttpRequestBuilder("https://readarr.servarr.com/v1/")
                .CreateFactory();

            MetadataOpenLibrary = new HttpRequestBuilder("https://openlibrary.org")
                .CreateFactory();

            MetadataRreadingGlasses = new HttpRequestBuilder("https://api.bookinfo.club/v1/{route}")
                .CreateFactory();

            Metadata = MetadataOpenLibrary;
        }

        public IHttpRequestBuilderFactory Services { get; }

        public IHttpRequestBuilderFactory Metadata { get; }

        public IHttpRequestBuilderFactory MetadataOpenLibrary { get; }

        public IHttpRequestBuilderFactory MetadataRreadingGlasses { get; }
    }
}
