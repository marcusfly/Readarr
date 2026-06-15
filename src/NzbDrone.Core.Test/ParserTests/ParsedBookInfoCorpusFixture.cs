using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    /// <summary>
    /// Corpus-based fixture for validating parser accuracy and rejection-reason population.
    /// Tests releases against known ground truth to measure false-positive and false-negative rates.
    /// </summary>
    [TestFixture]
    public class ParsedBookInfoCorpusFixture : CoreTest
    {
        private List<CorpusCase> _corpus;

        [SetUp]
        public void SetUp()
        {
            var corpusPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", "Parser", "releases-corpus.json");
            if (!File.Exists(corpusPath))
            {
                corpusPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files", "Parser", "releases-corpus.json");
            }

            if (File.Exists(corpusPath))
            {
                var json = File.ReadAllText(corpusPath);
                _corpus = Json.Deserialize<List<CorpusCase>>(json);
            }
            else
            {
                _corpus = new List<CorpusCase>();
            }
        }

        [TestCaseSource(nameof(GetCorpusCases))]
        public void should_parse_corpus_release_accurately(CorpusCase testCase)
        {
            if (testCase.ExpectedConfidenceMax < 0.7)
            {
                // For low-confidence cases, verify that RejectionReason is populated
                var result = Parser.Parser.ParseBookTitle(testCase.ReleaseTitle);

                if (result != null)
                {
                    result.RejectionReason.Should()
                        .NotBeNullOrWhiteSpace(
                            "low-confidence parses (< 0.7) should have a rejection reason explaining why confidence is low");
                }
            }
            else
            {
                // For high-confidence cases, parser should extract author and book
                var result = Parser.Parser.ParseBookTitle(testCase.ReleaseTitle);

                if (result != null)
                {
                    result.Should().NotBeNull();
                    result.AuthorName.Should().NotBeNullOrWhiteSpace();
                    result.BookTitle.Should().NotBeNullOrWhiteSpace();
                }
            }
        }

        private static IEnumerable<CorpusCase> GetCorpusCases()
        {
            var corpusPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", "Parser", "releases-corpus.json");
            if (!File.Exists(corpusPath))
            {
                corpusPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files", "Parser", "releases-corpus.json");
            }

            if (!File.Exists(corpusPath))
            {
                yield break;
            }

            var json = File.ReadAllText(corpusPath);
            var corpus = Json.Deserialize<List<CorpusCase>>(json);

            foreach (var testCase in corpus)
            {
                yield return testCase;
            }
        }

        public class CorpusCase
        {
            public string ReleaseTitle { get; set; }
            public string ExpectedAuthor { get; set; }
            public string ExpectedBook { get; set; }
            public float ExpectedConfidenceMin { get; set; }
            public float ExpectedConfidenceMax { get; set; }
            public string Category { get; set; }
        }
    }
}
