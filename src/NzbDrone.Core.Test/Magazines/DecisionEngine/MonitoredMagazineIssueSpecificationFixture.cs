using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine.Specifications.RssSync;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Magazines.DecisionEngine
{
    [TestFixture]
    public class MonitoredMagazineIssueSpecificationFixture : CoreTest<MonitoredMagazineIssueSpecification>
    {
        [Test]
        public void should_reject_unmonitored_issue_during_rss()
        {
            var remoteIssue = new RemoteMagazineIssue
            {
                Issue = new MagazineIssue { Monitored = false }
            };

            Subject.IsSatisfiedBy(remoteIssue, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_unmonitored_issue_during_interactive_search()
        {
            var remoteIssue = new RemoteMagazineIssue
            {
                Issue = new MagazineIssue { Monitored = false }
            };

            Subject.IsSatisfiedBy(remoteIssue, new MagazineIssueSearchCriteria { InteractiveSearch = true }).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_non_magazine_subjects()
        {
            Subject.IsSatisfiedBy(new RemoteBook(), null).Accepted.Should().BeTrue();
        }
    }
}
