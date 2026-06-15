using System.IO;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;

namespace NzbDrone.Core.Books
{
    public class MoveAuthorService : IExecute<MoveAuthorCommand>, IExecute<BulkMoveAuthorCommand>
    {
        private readonly IAuthorService _authorService;
        private readonly IBuildFileNames _filenameBuilder;
        private readonly IDiskProvider _diskProvider;
        private readonly IRootFolderWatchingService _rootFolderWatchingService;
        private readonly IDiskTransferService _diskTransferService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public MoveAuthorService(IAuthorService authorService,
                                 IBuildFileNames filenameBuilder,
                                 IDiskProvider diskProvider,
                                 IRootFolderWatchingService rootFolderWatchingService,
                                 IDiskTransferService diskTransferService,
                                 IEventAggregator eventAggregator,
                                 Logger logger)
        {
            _authorService = authorService;
            _filenameBuilder = filenameBuilder;
            _diskProvider = diskProvider;
            _rootFolderWatchingService = rootFolderWatchingService;
            _diskTransferService = diskTransferService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        private void MoveSingleAuthor(Author author, string sourcePath, string destinationPath, int? index = null, int? total = null, bool isAudiobook = false)
        {
            if (!_diskProvider.FolderExists(sourcePath))
            {
                _logger.Debug("Folder '{0}' for '{1}' does not exist, not moving.", sourcePath, author.Name);
                return;
            }

            if (index != null && total != null)
            {
                _logger.ProgressInfo("Moving {0} from '{1}' to '{2}' ({3}/{4})", author.Name, sourcePath, destinationPath, index + 1, total);
            }
            else
            {
                _logger.ProgressInfo("Moving {0} from '{1}' to '{2}'", author.Name, sourcePath, destinationPath);
            }

            if (sourcePath.PathEquals(destinationPath))
            {
                _logger.ProgressInfo("{0} is already in the specified location '{1}'.", author, destinationPath);
                return;
            }

            try
            {
                _rootFolderWatchingService.ReportFileSystemChangeBeginning(sourcePath, destinationPath);

                _diskTransferService.TransferFolder(sourcePath, destinationPath, TransferMode.Move);

                _logger.ProgressInfo("{0} moved successfully to {1}", author.Name, destinationPath);

                _eventAggregator.PublishEvent(new AuthorMovedEvent(author, sourcePath, destinationPath));
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "Unable to move author from '{0}' to '{1}'. Try moving files manually", sourcePath, destinationPath);

                if (isAudiobook)
                {
                    RevertAudiobookPath(author.Id, sourcePath);
                }
                else
                {
                    RevertPath(author.Id, sourcePath);
                }
            }
        }

        private void RevertPath(int authorId, string path)
        {
            var author = _authorService.GetAuthor(authorId);

            author.Path = path;
            _authorService.UpdateAuthor(author);
        }

        private void RevertAudiobookPath(int authorId, string path)
        {
            var author = _authorService.GetAuthor(authorId);

            author.AudiobookPath = path;
            _authorService.UpdateAuthor(author);
        }

        public void Execute(MoveAuthorCommand message)
        {
            var author = _authorService.GetAuthor(message.AuthorId);
            MoveSingleAuthor(author, message.SourcePath, message.DestinationPath);

            if (message.SourceAudiobookPath.IsNotNullOrWhiteSpace() &&
                message.DestinationAudiobookPath.IsNotNullOrWhiteSpace())
            {
                MoveSingleAuthor(author, message.SourceAudiobookPath, message.DestinationAudiobookPath, isAudiobook: true);
            }
        }

        public void Execute(BulkMoveAuthorCommand message)
        {
            var authorToMove = message.Author;
            var destinationRootFolder = message.DestinationRootFolder;
            var destinationAudiobookRootFolder = message.DestinationAudiobookRootFolder;

            _logger.ProgressInfo("Moving {0} author to '{1}'", authorToMove.Count, destinationRootFolder);

            for (var index = 0; index < authorToMove.Count; index++)
            {
                var s = authorToMove[index];
                var author = _authorService.GetAuthor(s.AuthorId);
                var authorFolder = _filenameBuilder.GetAuthorFolder(author);

                if (destinationRootFolder.IsNotNullOrWhiteSpace() &&
                    s.SourcePath.IsNotNullOrWhiteSpace())
                {
                    var destinationPath = Path.Combine(destinationRootFolder, authorFolder);
                    MoveSingleAuthor(author, s.SourcePath, destinationPath, index, authorToMove.Count);
                }

                if (destinationAudiobookRootFolder.IsNotNullOrWhiteSpace() &&
                    s.SourceAudiobookPath.IsNotNullOrWhiteSpace())
                {
                    var destinationAudiobookPath = Path.Combine(destinationAudiobookRootFolder, authorFolder);
                    MoveSingleAuthor(author, s.SourceAudiobookPath, destinationAudiobookPath, index, authorToMove.Count, true);
                }
            }

            _logger.ProgressInfo("Finished moving {0} author to '{1}'", authorToMove.Count, destinationRootFolder);
        }
    }
}
