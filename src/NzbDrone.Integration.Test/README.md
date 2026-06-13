# Readarr Integration Tests

This project contains end-to-end integration tests that run against a live Readarr
process started by the test harness.  They sit between unit tests (fast, no I/O) and
automation/Selenium tests (full browser).

---

## Contents

| Folder / File | What it covers |
|---|---|
| `ApiTests/` | HTTP API round-trips: authors, books, blocklist, calendar, commands, indexers, notifications, releases, root folders, wanted |
| `ApiTests/WantedTests/` | Wanted-missing and wanted-cutoff-unmet listing |
| `ApiTests/WorkflowTests/` | Happy-path E2E workflows (add author → refresh → verify books) |
| `Client/` | Typed REST client wrappers used by all fixtures |
| `IntegrationTest.cs` | Concrete base that starts/stops the Readarr process |
| `IntegrationTestBase.cs` | Abstract base with shared helpers (EnsureAuthor, EnsureTag, …) |

---

## How to run

### Prerequisites

* .NET 10 SDK (`dotnet --version` should report `10.x`)
* A built copy of Readarr in `_output/` (run `./build.sh --backend` first)
* Optional: a PostgreSQL server if you want to test the Postgres code path
  (set `READARR_TEST_POSTGRES_HOST`, `_USER`, `_PASSWORD` env vars)

### Unit tests only (fast, no Readarr process)

```bash
./test.sh Linux Unit Test
# or on Windows:
./test.sh Windows Unit Test
```

### Integration tests

```bash
./test.sh Linux Integration Test
# or on Windows:
./test.sh Windows Integration Test
```

The harness will:
1. Start a Readarr process on an incrementing port starting at 8787.
2. Wait for the task scheduler to initialise.
3. Run each fixture; each fixture that inherits `IntegrationTest` gets its own
   process instance.
4. Kill the process and (optionally) drop the Postgres database on teardown.

### Running a single fixture

```bash
dotnet test src/NzbDrone.Integration.Test/Readarr.Integration.Test.csproj \
  --filter "FullyQualifiedName~CriticalWorkflowFixture" \
  -- NUnit.Where="Category=IntegrationTest"
```

---

## What is covered

### `CriticalWorkflowFixture` (happy-path E2E)

| Test | What it exercises |
|---|---|
| `search_author_by_name_returns_results` | Metadata provider lookup by author name |
| `add_monitored_author_to_library` | POST /api/v1/author; persistence; monitored flag |
| `refresh_author_populates_books_in_library` | RefreshAuthorCommand; book import from metadata |
| `book_added_by_refresh_is_retrievable_by_id` | GET /api/v1/book?authorId=…; data integrity |
| `delete_author_removes_it_from_library` | DELETE /api/v1/author/{id}; library clean-up |

### Other API fixtures

Each file under `ApiTests/` covers CRUD + edge-cases for one resource type.
See the `[TestCase]` / `[Test]` annotations for the precise scenarios.

---

## What is stubbed vs. real

| Component | Status in integration tests |
|---|---|
| Readarr HTTP API | **Real** — a full process is started |
| SQLite database | **Real** — temp file created per run |
| PostgreSQL database | **Real** (optional) — enabled via env vars |
| Metadata provider (Open Library) | **Real** — live HTTP calls; tests are `[Ignore]`d in CI to avoid flakiness |
| Indexers | **Stubbed** — Newznab configured with RSS/search disabled |
| Download client | **Stubbed** — UsenetBlackhole pointed at a temp directory; `Enable=false` |
| File system | **Real** — temp directories under `TestContext.CurrentContext.TestDirectory` |
| SignalR | **Real** — `ConnectSignalR()` helper available in `IntegrationTestBase` |

> **Note on `[Ignore]` attributes:** Many fixtures are marked `[Ignore]` with an
> `Until` date because they depend on live metadata providers that were unavailable
> during earlier development sprints.  Remove or extend the `Until` date once the
> relevant provider is stable and reachable from CI.

---

## Adding new workflow tests

1. Create a new class in `ApiTests/WorkflowTests/` that inherits `IntegrationTest`.
2. Use `EnsureAuthor` / `EnsureNoAuthor` / `EnsureDownloadClient` helpers from
   `IntegrationTestBase` for setup and teardown.
3. Trigger background work with `Commands.PostAndWait(…)` and poll for side-effects
   with the `WaitForCompletion(predicate)` helper.
4. Clean up in a `[TearDown]` method so subsequent runs start clean.
5. Mark the fixture `[Ignore(…, Until = "…")]` if it requires a live external
   service that is not guaranteed to be reachable in CI.
