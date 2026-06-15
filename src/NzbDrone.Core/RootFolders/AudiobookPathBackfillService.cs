using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.RootFolders
{
    public class AudiobookPathBackfillService : IHandle<ApplicationStartedEvent>
    {
        private const string DEFAULT_AUDIOBOOK_ROOT_FOLDER_PATH = "/audiobooks";
        private const string AUDIOBOOK_ROOT_FOLDER_PATH_ENV_KEY = "READARR__ROOTFOLDER__AUDIOBOOKPATH";

        private readonly IAuthorService _authorService;
        private readonly IRootFolderService _rootFolderService;
        private readonly Logger _logger;

        public AudiobookPathBackfillService(IAuthorService authorService,
                                            IRootFolderService rootFolderService,
                                            Logger logger)
        {
            _authorService = authorService;
            _rootFolderService = rootFolderService;
            _logger = logger;
        }

        public void Handle(ApplicationStartedEvent message)
        {
            var rootFolders = _rootFolderService.All();
            var audiobookRootFolder = GetAudiobookRootFolder(rootFolders);

            if (audiobookRootFolder == null)
            {
                _logger.Debug("No audiobook root folder was available for audiobook-path backfill.");
                return;
            }

            var authors = _authorService.GetAllAuthors()
                .Where(x => x.AudiobookPath.IsNullOrWhiteSpace() && x.Path.IsNotNullOrWhiteSpace())
                .ToList();

            foreach (var author in authors)
            {
                try
                {
                    var sourceRootFolder = _rootFolderService.GetBestRootFolder(author.Path);

                    if (sourceRootFolder == null)
                    {
                        continue;
                    }

                    if (sourceRootFolder.Path.PathEquals(audiobookRootFolder.Path))
                    {
                        author.AudiobookPath = author.Path;
                    }
                    else
                    {
                        var relativePath = sourceRootFolder.Path.GetRelativePath(author.Path);

                        if (relativePath.IsNullOrWhiteSpace())
                        {
                            continue;
                        }

                        author.AudiobookPath = Path.Combine(audiobookRootFolder.Path, relativePath);
                    }

                    _authorService.UpdateAuthor(author);
                    _logger.Info("Backfilled audiobook path for {0} to {1}", author, author.AudiobookPath);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Unable to backfill audiobook path for {0}.", author);
                }
            }
        }

        private RootFolder GetAudiobookRootFolder(IReadOnlyCollection<RootFolder> rootFolders)
        {
            var configuredPath = Environment.GetEnvironmentVariable(AUDIOBOOK_ROOT_FOLDER_PATH_ENV_KEY);
            var audiobookRootPath = configuredPath.IsNotNullOrWhiteSpace()
                ? configuredPath
                : DEFAULT_AUDIOBOOK_ROOT_FOLDER_PATH;

            return rootFolders.FirstOrDefault(x => x.Path.PathEquals(audiobookRootPath));
        }
    }
}
