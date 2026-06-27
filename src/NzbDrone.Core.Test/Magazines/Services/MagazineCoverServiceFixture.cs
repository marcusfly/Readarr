using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Metadata;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Test.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NzbDrone.Core.Test.Magazines.Services
{
    [TestFixture]
    public class MagazineCoverServiceFixture : CoreTest<MagazineCoverService>
    {
        private string _coverPath;
        private string _issueCoverPath;

        [SetUp]
        public void SetUp()
        {
            WithTempAsAppPath();

            Mocker.GetMock<IAppFolderInfo>()
                .SetupGet(x => x.TempFolder)
                .Returns(TempFolder);

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FileExists(It.IsAny<string>()))
                .Returns((string path) => File.Exists(path));

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.CreateFolder(It.IsAny<string>()))
                .Callback((string path) => Directory.CreateDirectory(path));

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.OpenReadStream(It.IsAny<string>()))
                .Returns((string path) => File.OpenRead(path));

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FileGetLastWrite(It.IsAny<string>()))
                .Returns((string path) => File.GetLastWriteTimeUtc(path));

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.GetFileSize(It.IsAny<string>()))
                .Returns((string path) => new FileInfo(path).Length);

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FileSetLastWriteTime(It.IsAny<string>(), It.IsAny<DateTime>()))
                .Callback((string path, DateTime time) => File.SetLastWriteTimeUtc(path, time));

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.DeleteFile(It.IsAny<string>()))
                .Callback((string path) =>
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                });

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.ReadAllText(It.IsAny<string>()))
                .Returns((string path) => File.ReadAllText(path));

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()))
                .Callback((string path, string contents) =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, contents);
                });

            _coverPath = Path.Combine(TempFolder, "MediaCover", "Magazines", "7", "cover.jpg");
            _issueCoverPath = Path.Combine(TempFolder, "MediaCover", "MagazineIssues", "17", "cover.jpg");

            Mocker.GetMock<IMapCoversToLocal>()
                .Setup(x => x.GetCoverPath(7, MediaCoverEntity.Magazine, MediaCoverTypes.Cover, ".jpg", null))
                .Returns(_coverPath);

            Mocker.GetMock<IMapCoversToLocal>()
                .Setup(x => x.ConvertToLocalUrls(7, MediaCoverEntity.Magazine, It.IsAny<IEnumerable<MediaCover.MediaCover>>()))
                .Callback((int id, MediaCoverEntity entity, IEnumerable<MediaCover.MediaCover> covers) =>
                {
                    foreach (var cover in covers)
                    {
                        cover.RemoteUrl = cover.Url;
                        cover.Url = $"/MediaCover/Magazines/{id}/cover.jpg";
                    }
                });

            Mocker.GetMock<IMapCoversToLocal>()
                .Setup(x => x.GetCoverPath(17, MediaCoverEntity.MagazineIssue, MediaCoverTypes.Cover, ".jpg", null))
                .Returns(_issueCoverPath);

            Mocker.GetMock<IMapCoversToLocal>()
                .Setup(x => x.ConvertToLocalUrls(17, MediaCoverEntity.MagazineIssue, It.IsAny<IEnumerable<MediaCover.MediaCover>>()))
                .Callback((int id, MediaCoverEntity entity, IEnumerable<MediaCover.MediaCover> covers) =>
                {
                    foreach (var cover in covers)
                    {
                        cover.RemoteUrl = cover.Url;
                        cover.Url = $"/MediaCover/MagazineIssues/{id}/cover.jpg";
                    }
                });
        }

        [Test]
        public void should_generate_cover_from_direct_image_file()
        {
            var imagePath = Path.Combine(TempFolder, "issue-cover.png");

            using (var image = new Image<Rgba32>(300, 500, new Rgba32(200, 40, 40)))
            {
                image.SaveAsPng(imagePath);
            }

            var magazine = new Magazine
            {
                Id = 7,
                Title = "Cover Test"
            };

            var issue = new MagazineIssue
            {
                MagazineId = 7,
                IssueFiles = new LazyLoaded<List<MagazineIssueFile>>(new List<MagazineIssueFile>
                {
                    new MagazineIssueFile
                    {
                        MagazineId = 7,
                        Path = imagePath,
                        DateAdded = DateTime.UtcNow
                    }
                })
            };

            var result = Subject.GetImages(magazine, new[] { issue });

            result.Should().HaveCount(1);
            result[0].Url.Should().Be("/MediaCover/Magazines/7/cover.jpg");
            File.Exists(_coverPath).Should().BeTrue();
            new FileInfo(_coverPath).Length.Should().BeGreaterThan(0);
        }

        [Test]
        public void should_generate_cover_for_individual_issue_from_first_page()
        {
            var imagePath = Path.Combine(TempFolder, "issue-page.png");

            using (var image = new Image<Rgba32>(320, 480, new Rgba32(40, 120, 220)))
            {
                image.SaveAsPng(imagePath);
            }

            var issue = new MagazineIssue
            {
                Id = 17,
                MagazineId = 7,
                IssueFiles = new LazyLoaded<List<MagazineIssueFile>>(new List<MagazineIssueFile>
                {
                    new MagazineIssueFile
                    {
                        Id = 9,
                        MagazineIssueId = 17,
                        MagazineId = 7,
                        Path = imagePath,
                        DateAdded = DateTime.UtcNow
                    }
                })
            };

            var result = Subject.GetIssueImages(issue);

            result.Should().HaveCount(1);
            result[0].Url.Should().Be("/MediaCover/MagazineIssues/17/cover.jpg");
            File.Exists(_issueCoverPath).Should().BeTrue();
            new FileInfo(_issueCoverPath).Length.Should().BeGreaterThan(0);
        }

        [Test]
        public void should_fallback_to_authority_cover_when_no_issue_files_exist()
        {
            var magazine = new Magazine
            {
                Id = 7,
                Title = "Playboy"
            };

            Mocker.GetMock<IMagazineTitleAuthorityProvider>()
                .Setup(x => x.LookupByTitleAsync("Playboy", default))
                .Returns(Task.FromResult(new MagazineAuthorityResult
                {
                    CanonicalTitle = "Playboy",
                    ImageUrl = "https://images.example/playboy-cover.jpg"
                }));

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.DownloadFile("https://images.example/playboy-cover.jpg", _coverPath, null))
                .Callback(() =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_coverPath));
                    using var image = new Image<Rgba32>(300, 500, new Rgba32(80, 80, 80));
                    image.SaveAsJpeg(_coverPath);
                });

            var result = Subject.GetImages(magazine, Array.Empty<MagazineIssue>());

            result.Should().HaveCount(1);
            result[0].Url.Should().Be("/MediaCover/Magazines/7/cover.jpg");
            result[0].RemoteUrl.Should().Be("https://images.example/playboy-cover.jpg");
            File.Exists(_coverPath).Should().BeTrue();
        }

        [Test]
        public void should_fallback_to_authority_cover_when_local_magazine_cover_generation_fails()
        {
            var archivePath = Path.Combine(TempFolder, "broken.cbz");
            File.WriteAllText(archivePath, "not a zip archive");

            var magazine = new Magazine
            {
                Id = 7,
                Title = "Playboy"
            };

            var issue = new MagazineIssue
            {
                MagazineId = 7,
                IssueFiles = new LazyLoaded<List<MagazineIssueFile>>(new List<MagazineIssueFile>
                {
                    new MagazineIssueFile
                    {
                        MagazineId = 7,
                        Path = archivePath,
                        DateAdded = DateTime.UtcNow
                    }
                })
            };

            Mocker.GetMock<IMagazineTitleAuthorityProvider>()
                .Setup(x => x.LookupByTitleAsync("Playboy", default))
                .Returns(Task.FromResult(new MagazineAuthorityResult
                {
                    CanonicalTitle = "Playboy",
                    ImageUrl = "https://images.example/playboy-cover.jpg"
                }));

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.DownloadFile("https://images.example/playboy-cover.jpg", _coverPath, null))
                .Callback(() =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_coverPath));
                    using var image = new Image<Rgba32>(300, 500, new Rgba32(80, 80, 80));
                    image.SaveAsJpeg(_coverPath);
                });

            var result = Subject.GetImages(magazine, new[] { issue });

            result.Should().HaveCount(1);
            result[0].Url.Should().Be("/MediaCover/Magazines/7/cover.jpg");
            result[0].RemoteUrl.Should().Be("https://images.example/playboy-cover.jpg");
            File.Exists(_coverPath).Should().BeTrue();
            Mocker.GetMock<IHttpClient>()
                .Verify(x => x.DownloadFile("https://images.example/playboy-cover.jpg", _coverPath, null), Times.Once());
        }

        [Test]
        public void should_refresh_existing_magazine_cover_when_authority_source_differs()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_coverPath));

            using (var image = new Image<Rgba32>(300, 500, new Rgba32(40, 40, 40)))
            {
                image.SaveAsJpeg(_coverPath);
            }

            var magazine = new Magazine
            {
                Id = 7,
                Title = "Playboy"
            };

            Mocker.GetMock<IMagazineTitleAuthorityProvider>()
                .Setup(x => x.LookupByTitleAsync("Playboy", default))
                .Returns(Task.FromResult(new MagazineAuthorityResult
                {
                    CanonicalTitle = "Playboy",
                    ImageUrl = "https://images.example/playboy-cover.jpg"
                }));

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.DownloadFile("https://images.example/playboy-cover.jpg", _coverPath, null))
                .Callback(() =>
                {
                    using var image = new Image<Rgba32>(300, 500, new Rgba32(220, 40, 40));
                    image.SaveAsJpeg(_coverPath);
                });

            var result = Subject.GetImages(magazine, Array.Empty<MagazineIssue>());

            result.Should().HaveCount(1);
            result[0].RemoteUrl.Should().Be("https://images.example/playboy-cover.jpg");
            Mocker.GetMock<IHttpClient>()
                .Verify(x => x.DownloadFile("https://images.example/playboy-cover.jpg", _coverPath, null), Times.Once());
        }

        [Test]
        public void should_reuse_existing_magazine_cover_when_authority_cover_is_unavailable()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_coverPath));

            using (var image = new Image<Rgba32>(300, 500, new Rgba32(40, 40, 40)))
            {
                image.SaveAsJpeg(_coverPath);
            }

            var magazine = new Magazine
            {
                Id = 7,
                Title = "Existing Cover"
            };

            Mocker.GetMock<IMagazineTitleAuthorityProvider>()
                .Setup(x => x.LookupByTitleAsync("Existing Cover", default))
                .Returns(Task.FromResult(new MagazineAuthorityResult
                {
                    CanonicalTitle = "Existing Cover"
                }));

            var result = Subject.GetImages(magazine, Array.Empty<MagazineIssue>());

            result.Should().HaveCount(1);
            result[0].Url.Should().StartWith("/MediaCover/Magazines/7/cover.jpg");
            Mocker.GetMock<IMagazineTitleAuthorityProvider>()
                .Verify(x => x.LookupByTitleAsync("Existing Cover", default), Times.Once());
        }

        [Test]
        public void should_reuse_existing_magazine_cover_when_local_generation_fails_and_authority_cover_is_unavailable()
        {
            var archivePath = Path.Combine(TempFolder, "broken.cbz");
            File.WriteAllText(archivePath, "not a zip archive");

            Directory.CreateDirectory(Path.GetDirectoryName(_coverPath));

            using (var image = new Image<Rgba32>(300, 500, new Rgba32(40, 40, 40)))
            {
                image.SaveAsJpeg(_coverPath);
            }

            File.SetLastWriteTimeUtc(_coverPath, DateTime.UtcNow.AddMinutes(-5));

            var magazine = new Magazine
            {
                Id = 7,
                Title = "Existing Cover"
            };

            var issue = new MagazineIssue
            {
                MagazineId = 7,
                IssueFiles = new LazyLoaded<List<MagazineIssueFile>>(new List<MagazineIssueFile>
                {
                    new MagazineIssueFile
                    {
                        MagazineId = 7,
                        Path = archivePath,
                        DateAdded = DateTime.UtcNow
                    }
                })
            };

            Mocker.GetMock<IMagazineTitleAuthorityProvider>()
                .Setup(x => x.LookupByTitleAsync("Existing Cover", default))
                .Returns(Task.FromResult(new MagazineAuthorityResult
                {
                    CanonicalTitle = "Existing Cover"
                }));

            var result = Subject.GetImages(magazine, new[] { issue });

            result.Should().HaveCount(1);
            result[0].Url.Should().StartWith("/MediaCover/Magazines/7/cover.jpg");
            Mocker.GetMock<IMagazineTitleAuthorityProvider>()
                .Verify(x => x.LookupByTitleAsync("Existing Cover", default), Times.Once());
        }

        [Test]
        public void should_log_clear_warning_when_pdftoppm_is_not_available()
        {
            var pdfPath = Path.Combine(TempFolder, "missing-tool.pdf");
            File.WriteAllText(pdfPath, "%PDF-1.4");
            var destinationPath = Path.Combine(TempFolder, "missing-tool.jpg");
            var originalPath = Environment.GetEnvironmentVariable("PATH");
            var generateCover = typeof(MagazineCoverService).GetMethod("GenerateCover", BindingFlags.Instance | BindingFlags.NonPublic);

            try
            {
                Environment.SetEnvironmentVariable("PATH", TempFolder);

                var act = () => generateCover.Invoke(Subject, new object[] { pdfPath, destinationPath });

                var exception = act.Should().Throw<TargetInvocationException>().Which;
                exception.InnerException.Should().NotBeNull();
                exception.InnerException.Message.Should().Be("pdftoppm is not available. Install poppler-utils and ensure 'pdftoppm' is on PATH.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
            }
        }
    }
}
