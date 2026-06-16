using NLog;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.RssSync
{
    public class MonitoredMagazineIssueSpecification : IDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public MonitoredMagazineIssueSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public Decision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria)
        {
            if (subject is not RemoteMagazineIssue remoteIssue)
            {
                return Decision.Accept();
            }

            if (searchCriteria?.InteractiveSearch == true || searchCriteria?.UserInvokedSearch == true)
            {
                _logger.Debug("Skipping monitored check during interactive or user-invoked magazine search");
                return Decision.Accept();
            }

            if (remoteIssue.Issue?.Monitored == false)
            {
                _logger.Debug("{0} is present in the DB but not monitored. Rejecting.", remoteIssue.Issue.ReleaseTitle);
                return Decision.Reject("Magazine issue is not monitored");
            }

            return Decision.Accept();
        }
    }
}
