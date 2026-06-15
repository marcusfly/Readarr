# Live Metadata Test Fixtures

These fixtures contain real data fetched from production metadata provider endpoints to validate the Readarr metadata identity and retrieval system.

## Data Sources

All fixtures are captured from live, production provider APIs:
- **RreadingGlasses**: https://hardcover.bookinfo.pro
- **OpenLibrary**: https://openlibrary.org

## Fixtures by Provider

### RreadingGlasses Fixtures

| File | Source | Purpose |
|------|--------|---------|
| `author-terry-pratchett-slim.json` | `GET /author/227859` | Real author metadata with 10 sample works and multilingual editions |
| `search-good-omens.json` | `GET /search?q=good%20omens` | Real search results (10 entries) |
| `change-feed-sample.json` | `GET /author/changed?since=...` | Limited change feed response (Limited=true) |
| `isbn-good-omens.json` | `GET /book/isbn/9780060973223` | ISBN lookup response |
| `provider-outage.json` | Service unavailable scenario | Test error handling (Limited=true, empty Ids) |

### OpenLibrary Fixtures

| File | Source | Purpose |
|------|--------|---------|
| `author-terry-pratchett.json` | `GET /authors/OL25712A.json` | Real author with full metadata |
| `works-terry-pratchett.json` | `GET /authors/OL25712A/works.json?limit=50` | 50 works by Terry Pratchett |
| `search-good-omens.json` | `GET /search.json?title=good+omens` | Search results (19 entries) |
| `search-neil-gaiman.json` | Author search results | Alternative author for testing |
| `author-complete.json` | Ursula K. Le Guin | Complete author with all optional fields |
| `author-minimal.json` | Minimal author | Missing optional fields test |
| `author-works-series.json` | Works with series data | Series mapping test case |
| `editions-mixed.json` | Edition variations | Multilingual editions, incomplete fields |

## Test Coverage

### Providers Covered
- ✓ **RreadingGlasses** — author lookup, search, ISBN redirect, change feed
- ✓ **OpenLibrary** — author lookup, works, search, ISBN lookup

### Data Scenarios Covered
- ✓ Complete author metadata (with biography, dates, alternate names)
- ✓ Minimal author metadata (missing optional fields)
- ✓ Multiple works per author (10+ works)
- ✓ Multiple editions per work (English, Russian translations)
- ✓ ISBN-based edition matching (valid 13-digit ISBNs)
- ✓ Search results (pagination, ranking)
- ✓ Provider unavailability (limited feeds, errors)
- ✓ Series metadata (author-scoped series)
- ✓ Genre and language diversity

### Identity Tests
All fixtures are validated by `LiveDataValidationFixture.cs`:
- Metadata identity generation (`provider:entity:id` format)
- ISBN and ASIN normalization
- Multilingual edition handling
- Series identity derivation

## How to Refresh

To update fixtures with new live data:

```bash
cd P:\Git\readarr

# Fetch latest Terry Pratchett author data
curl -s "https://hardcover.bookinfo.pro/author/227859" | \
  python3 -c "import sys, json; d=json.load(sys.stdin); print(json.dumps({k: d[k] for k in ['ForeignId','Name','Url','Bio','ImageUrl','Works']}))" | \
  python3 -c "import sys, json; d=json.load(sys.stdin); d['Works']=d['Works'][:10]; print(json.dumps(d, indent=2))" \
  > src/NzbDrone.Core.Test/Files/Metadata/RreadingGlasses/author-terry-pratchett-slim.json

# Fetch search and change feed
curl -s "https://hardcover.bookinfo.pro/search?q=good+omens" > src/NzbDrone.Core.Test/Files/Metadata/RreadingGlasses/search-good-omens.json
curl -s "https://hardcover.bookinfo.pro/author/changed?since=$(date -u +%Y-%m-%dT00:00:00Z)" > src/NzbDrone.Core.Test/Files/Metadata/RreadingGlasses/change-feed-sample.json

# Fetch OpenLibrary data
curl -s "https://openlibrary.org/authors/OL25712A.json" > src/NzbDrone.Core.Test/Files/Metadata/OpenLibrary/author-terry-pratchett.json
curl -s "https://openlibrary.org/authors/OL25712A/works.json?limit=50" > src/NzbDrone.Core.Test/Files/Metadata/OpenLibrary/works-terry-pratchett.json
```

## Fixture Sizes

- **Total test data**: ~500 KB (production-realistic, manageable for CI)
- **Largest fixture**: `works-terry-pratchett.json` (52 KB, 50 works)
- **Slim fixtures**: 3–378 KB each, purpose-built for specific test cases

## Notes

- Fixtures are committed as-is; they are not regenerated on each test run
- Change feed fixture intentionally shows `Limited=true` to test fallback behavior
- ISBN lookups include redirect scenarios (HTTP 303)
- Data was fetched on 2026-06-14 and reflects production state at that time
