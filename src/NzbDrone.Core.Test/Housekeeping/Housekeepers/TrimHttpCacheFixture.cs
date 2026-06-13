using System;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Housekeeping.Housekeepers;
using NzbDrone.Core.Http;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    [TestFixture]
    public class TrimHttpCacheFixture : CoreTest<TrimHttpCache>
    {
        [Test]
        public void should_preserve_responses_within_stale_retention_window()
        {
            var before = DateTime.UtcNow.AddDays(-CachedHttpResponse.StaleRetentionDays).AddSeconds(-1);

            Subject.Clean();

            var after = DateTime.UtcNow.AddDays(-CachedHttpResponse.StaleRetentionDays).AddSeconds(1);
            Mocker.GetMock<ICachedHttpResponseRepository>()
                  .Verify(v => v.DeleteOlderThan(It.Is<DateTime>(cutoff => cutoff >= before && cutoff <= after)));
        }

        [Test]
        public void should_vacuum_cache_after_trimming()
        {
            Subject.Clean();

            Mocker.GetMock<ICacheDatabase>().Verify(v => v.Vacuum());
        }
    }
}
