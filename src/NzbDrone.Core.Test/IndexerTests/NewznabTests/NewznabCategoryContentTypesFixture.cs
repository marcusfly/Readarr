using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.ContentTypes;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.NewznabTests
{
    public class NewznabCategoryContentTypesFixture : CoreTest
    {
        [TestCase(3030, LibraryContentType.Audiobook)]
        [TestCase(7020, LibraryContentType.Book)]
        [TestCase(7030, LibraryContentType.Comic)]
        [TestCase(7040, LibraryContentType.Magazine)]
        public void should_map_known_readarr_categories_to_content_types(int categoryId, LibraryContentType expected)
        {
            NewznabCategoryContentTypes.GetContentTypes(categoryId).Should().Be(expected);
        }

        [Test]
        public void should_map_book_parent_category_to_all_book_family_content_types()
        {
            NewznabCategoryContentTypes.GetContentTypes(7000)
                .Should()
                .Be(LibraryContentType.Book | LibraryContentType.Comic | LibraryContentType.Magazine);
        }

        [Test]
        public void should_infer_content_types_from_unknown_category_names()
        {
            NewznabCategoryContentTypes.GetContentTypes(9999, "Graphic Novels and Comics")
                .Should()
                .Be(LibraryContentType.Comic);
        }
    }
}
