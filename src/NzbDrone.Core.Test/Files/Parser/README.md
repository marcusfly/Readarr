# Release Parser Corpus

This directory contains test cases and fixtures for validating parser accuracy and rejection-reason population.

## Files

### releases-corpus.json

A structured corpus of real and synthetic release titles with expected parse outcomes.

**Schema:**
- `ReleaseTitle`: The release title to parse
- `ExpectedAuthor`: Expected author name (if parseable)
- `ExpectedBook`: Expected book title (if parseable)
- `ExpectedConfidenceMin`: Minimum confidence score expected
- `ExpectedConfidenceMax`: Maximum confidence score expected
- `Category`: Release format category (standard, audiobook, ebook, discography, edition_variant, missing_year, etc.)

**Categories:**
- `standard`: Full author-book-year releases
- `audiobook`: Audiobook-specific formats (MP3, M4B, Unabridged, etc.)
- `ebook`: E-reader formats (EPUB, PDF, MOBI)
- `discography`: Author discography collections
- `edition_variant`: Special editions (Annotated, Deluxe, etc.)
- `missing_year`: Releases without publication year
- `low_confidence`: Intentionally malformed or ambiguous titles

## Usage

### Adding Test Cases

1. Add a new entry to `releases-corpus.json` with real or representative release title
2. Run `ParsedBookInfoCorpusFixture` — it will automatically load and test all cases
3. Cases with `ExpectedConfidenceMax < 0.7` will verify that `RejectionReason` is populated
4. Cases with `ExpectedConfidenceMax >= 0.7` will verify successful parsing

### Measuring Accuracy

The corpus enables measurement of:
- **True Positive Rate (TPR)**: Correct parses / expected parseable titles
- **False Positive Rate (FPR)**: Incorrect parses / total parses
- **False Negative Rate (FNR)**: Missed parses / expected parseable titles
- **Rejection Reason Coverage**: % of low-confidence cases with populated RejectionReason

Run all parser tests to get baseline metrics:
```bash
dotnet test src/NzbDrone.Core.Test/Readarr.Core.Test.csproj -c Release --filter "ParsedBookInfo"
```

## Future Expansion

Expand corpus with:
- Real releases from known indexers (e.g., 50+ untested titles)
- Edge cases (very long titles, special characters, non-ASCII)
- Regional variants (multilingual, different naming conventions)
- Historical releases (pre-2000, out-of-print editions)
