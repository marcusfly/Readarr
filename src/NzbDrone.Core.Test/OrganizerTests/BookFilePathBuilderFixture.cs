using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.OrganizerTests
{
    [TestFixture]
    public class BookFilePathBuilderFixture : CoreTest<FileNameBuilder>
    {
        private Author _author;
        private Edition _edition;

        [SetUp]
        public void Setup()
        {
            _author = Builder<Author>.CreateNew()
                .With(a => a.Path = @"/books/Fake Author".AsOsAgnostic())
                .With(a => a.AudiobookPath = @"/audiobooks/Fake Author".AsOsAgnostic())
                .Build();

            _edition = Builder<Edition>.CreateNew()
                .With(e => e.Book = Builder<Book>.CreateNew().Build())
                .Build();
        }

        [Test]
        public void should_build_ebook_file_path_under_author_path()
        {
            var bookFile = Builder<BookFile>.CreateNew()
                .With(f => f.Quality = new QualityModel(Quality.EPUB))
                .With(f => f.Path = @"/downloads/book.epub".AsOsAgnostic())
                .Build();

            Subject.BuildBookFilePath(_author, _edition, "book", ".epub", bookFile)
                .Should().Be(@"/books/Fake Author/book.epub".AsOsAgnostic());
        }

        [Test]
        public void should_build_audiobook_file_path_under_audiobook_path()
        {
            var bookFile = Builder<BookFile>.CreateNew()
                .With(f => f.Quality = new QualityModel(Quality.M4B))
                .With(f => f.Path = @"/downloads/book.m4b".AsOsAgnostic())
                .Build();

            Subject.BuildBookFilePath(_author, _edition, "book", ".m4b", bookFile)
                .Should().Be(@"/audiobooks/Fake Author/book.m4b".AsOsAgnostic());
        }
    }
}
