using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Core.Magazines.Events;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Magazines
{
    public interface IMagazineService
    {
        Magazine GetMagazine(int id);
        List<Magazine> GetAllMagazines();
        Magazine AddMagazine(Magazine magazine);
        Magazine UpdateMagazine(Magazine magazine);
        void DeleteMagazine(int magazineId, bool deleteFiles);
        Magazine FindByTitle(string title);
        Magazine FindByNormalizedTitle(string title);
    }

    public class MagazineService : IMagazineService
    {
        private readonly IMagazineRepository _magazineRepository;
        private readonly IEventAggregator _eventAggregator;
        private readonly IBuildMagazinePaths _magazinePathBuilder;
        private readonly Logger _logger;

        public MagazineService(IMagazineRepository magazineRepository,
                               IBuildMagazinePaths magazinePathBuilder,
                               IEventAggregator eventAggregator,
                               Logger logger)
        {
            _magazineRepository = magazineRepository;
            _magazinePathBuilder = magazinePathBuilder;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public List<Magazine> GetAllMagazines()
        {
            return _magazineRepository.All().ToList();
        }

        public Magazine GetMagazine(int id)
        {
            return _magazineRepository.Get(id);
        }

        public Magazine AddMagazine(Magazine magazine)
        {
            if (magazine == null)
            {
                throw new ArgumentNullException(nameof(magazine));
            }

            if (string.IsNullOrWhiteSpace(magazine.Title))
            {
                throw new InvalidOperationException("Cannot add a magazine without a title");
            }

            if (!Path.IsPathRooted(magazine.Path) && !string.IsNullOrWhiteSpace(magazine.RootFolderPath))
            {
                magazine.Path = _magazinePathBuilder.BuildPath(magazine);
            }

            _magazineRepository.Insert(magazine);

            if (magazine.Added == default)
            {
                magazine.Added = DateTime.UtcNow;
                _magazineRepository.Update(magazine);
            }

            _logger.Info("Added magazine {0}", magazine);
            _eventAggregator.PublishEvent(new MagazineAddedEvent(magazine));

            return magazine;
        }

        public Magazine UpdateMagazine(Magazine magazine)
        {
            var existing = GetMagazine(magazine.Id);

            var updated = _magazineRepository.Update(magazine);

            _logger.Info("Updated magazine {0}", magazine);
            _eventAggregator.PublishEvent(new MagazineUpdatedEvent(updated, existing));

            return updated;
        }

        public void DeleteMagazine(int magazineId, bool deleteFiles)
        {
            var magazine = GetMagazine(magazineId);

            _magazineRepository.Delete(magazineId);

            _logger.Info("Deleted magazine {0}", magazine);
            _eventAggregator.PublishEvent(new MagazineDeletedEvent(magazine, deleteFiles));
        }

        public Magazine FindByTitle(string title)
        {
            return _magazineRepository.GetByTitle(title);
        }

        public Magazine FindByNormalizedTitle(string title)
        {
            return _magazineRepository.GetByNormalizedTitle(title);
        }
    }
}
