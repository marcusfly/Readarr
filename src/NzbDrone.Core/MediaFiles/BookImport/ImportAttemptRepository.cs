using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles.BookImport
{
    public interface IImportAttemptRepository : IBasicRepository<ImportAttempt>
    {
        /// <summary>
        /// Returns all attempts that were InProgress when the process last terminated
        /// (i.e. attempts that were never marked Completed, Failed, or RolledBack).
        /// </summary>
        List<ImportAttempt> FindInProgress();

        /// <summary>Returns the most recent attempt for the given source path, or null.</summary>
        ImportAttempt FindBySourcePath(string sourcePath);
    }

    public class ImportAttemptRepository : BasicRepository<ImportAttempt>, IImportAttemptRepository
    {
        public ImportAttemptRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<ImportAttempt> FindInProgress()
        {
            return Query(x => x.Status == ImportAttemptStatus.InProgress).ToList();
        }

        public ImportAttempt FindBySourcePath(string sourcePath)
        {
            return Query(x => x.SourcePath == sourcePath)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefault();
        }
    }
}
