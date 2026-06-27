using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Magazines.Metadata;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Magazines.Metadata
{
    [TestFixture]
    public class IssnLTableImporterFixture : CoreTest<IssnLTableImporter>
    {
        private string _tablePath;

        [SetUp]
        public void SetUp()
        {
            _tablePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "issn-l-table.tsv");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tablePath))
            {
                File.Delete(_tablePath);
            }
        }

        [Test]
        public void should_resolve_issn_and_siblings_from_tsv()
        {
            File.WriteAllText(_tablePath, @"2049-3630	2049-3630
2049-3681	2049-3630
0028-0836	0028-0836
");

            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.IssnLTablePath)
                .Returns(_tablePath);

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FileExists(_tablePath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.ReadAllText(_tablePath))
                .Returns(File.ReadAllText(_tablePath));

            Subject.GetIssnL("2049-3681").Should().Be("2049-3630");
            Subject.GetSiblings("2049-3681").Should().BeEquivalentTo("2049-3630", "2049-3681");
        }

        [Test]
        public void should_treat_missing_or_empty_path_as_noop()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.IssnLTablePath)
                .Returns(string.Empty);

            Subject.GetIssnL("2049-3630").Should().BeNull();
            Subject.GetSiblings("2049-3630").Should().BeEmpty();

            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.IssnLTablePath)
                .Returns(_tablePath);

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FileExists(_tablePath))
                .Returns(false);

            Subject.GetIssnL("2049-3630").Should().BeNull();
            Subject.GetSiblings("2049-3630").Should().BeEmpty();
        }
    }
}
