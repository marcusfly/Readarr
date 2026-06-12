using Readarr.Api.V1.Indexers;
using RestSharp;

namespace NzbDrone.Integration.Test.Client
{
    public class ReleasePushClient : ClientBase<ReleaseResource>
    {
        public ReleasePushClient(RestClient restClient, string apiKey)
            : base(restClient, apiKey, "release/push")
        {
        }
    }
}
