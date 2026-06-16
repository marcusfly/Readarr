# Magazine Support: Verified Gap Analysis and Codex Continuation Prompt

_Written: 2026-06-15 (updated after worktree cleanup). Read `AGENTS.md` before starting._
_All gaps verified against the current `develop` branch._

---

## Current State (develop branch)

The magazine implementation on `develop` is a solid first pass but has four concrete gaps before the feature is production-complete. Two things I previously flagged as missing were wrong:

- `IBuildMagazinePaths` / `MagazinePathBuilder` **IS present** — defined at the bottom of `src/NzbDrone.Core/Magazines/Utilities/MagazineTitleNormalizer.cs`
- `magazines_seed.json` embedded resource **IS registered** in `src/NzbDrone.Core/Readarr.Core.csproj`

The automatic disk-scan pipeline is intentionally absent: Codex chose to implement a manual import endpoint (`MagazineImportService` + `MagazineImportController`) instead, and logged the automatic-scan gap as BLI014.

---

## What is Complete

### Phase 0 — Contracts
- Migration 045 (4 tables, four-tuple unique index on `MagazineIssues` — weekly magazine support)
- All entity stubs, `ParsedMagazineIssueInfo`, `IMagazineTitleAuthorityProvider`, `MagazineIssueSearchCriteria`
- Command stubs, API DTOs
- `SearchCriteriaBase.IndexerCategories` virtual override; `MagazineIssueSearchCriteria` returns `[7000, 7010]` (Blocker 2 fix applied)
- `MigrationIntegrityCheck` and `SCHEMA.md` updated; `TableMapping.cs` wired

### Stream A — Core Domain
- All repositories, services, events, handlers (including `DeleteMagazineService`, `MoveMagazineService`, `RescanMagazineService`)
- `MagazineTitleNormalizer`, `MagazinePathBuilder` (`IBuildMagazinePaths`)

### Stream B — Indexer/Search (partial — see Gap 2 below)
- `MagazineIssueSearchService` wired to `ISearchForReleases`
- `ReleaseSearchService` extended with `MagazineIssueSearch`
- `NewznabRequestGenerator` reads `criteria.IndexerCategories` (B-0 spike: implicit pass-through via virtual override)

### Stream D — Metadata
- `DefaultMagazineTitleAuthorityProvider` (single class: manual aliases → seed cache → Wikidata HTTP)
- `MagazineManualAliasStore`, `MagazineSeedCache`, `magazines_seed.json` embedded resource
- `DisableWikidataLookup` config key; CBR/CBZ/CBT quality entries added

### Stream E — API (complete + extras)
- `MagazineController`, `MagazineLookupController`, `MagazineIssueController`, `MagazineIssueFileController`, `MagazineRootFolderController`
- `MagazineImportController` + `MagazineImportService` (manual import endpoint, BLI014 partial resolution)

### Stream F — Frontend (add flow only — see Gap 3 below)
- `AddNewMagazineModal`, `AddNewMagazineModalContent`, `AddNewMagazineModalContentConnector`
- `AddMagazineOptionsForm`, `AddNewMagazineSearchResult`, `AddNewMagazineSearchResultConnector`
- `getNewMagazine.js`, `monitorOptions.js`; `searchActions.js` wired with `addMagazine` thunk
- `commandNames.js` updated

### Tests
- `MagazineIssueSearchDefinitionFixture`, `MagazineIssueSearchServiceFixture`
- `DeleteMagazineServiceFixture`, `MoveMagazineServiceFixture`, `RescanMagazineServiceFixture`
- `DefaultMagazineTitleAuthorityProviderFixture`, `MetadataProviderSelectorFixture`
- `MagazineFormatDetectorFixture`, `MagazineImportServiceFixture`

---

## Remaining Gaps (ordered by severity)

### Gap 1 — Download Decision Pipeline Broken (Stream B residual)

Magazine releases found via search cannot be properly classified, matched to a library issue, or gated by monitoring state. Three files are missing:

**`src/NzbDrone.Core/Parser/Model/RemoteMagazineIssue.cs`**
```csharp
namespace NzbDrone.Core.Parser.Model
{
    public class RemoteMagazineIssue : RemoteBook
    {
        public Magazine Magazine { get; set; }
        public MagazineIssue Issue { get; set; }
        public ParsedMagazineIssueInfo ParsedMagazineIssueInfo { get; set; }
    }
}
```

**`src/NzbDrone.Core/Parser/MagazineParsingService.cs`**
- Interface: `IMagazineParsingService`
- `Map(ParsedMagazineIssueInfo parsedInfo, SearchCriteriaBase searchCriteria = null)` → resolves `Magazine` by `parsedInfo.NormalizedMagazineTitle` via `IMagazineService.FindByNormalizedTitle`, matches `MagazineIssue` by `(year, month, day)` via `IMagazineIssueService`, returns `RemoteMagazineIssue`
- When `searchCriteria` is `MagazineIssueSearchCriteria`, use its `.Magazine` / `.Issue` directly (no DB lookup)

**`src/NzbDrone.Core/DecisionEngine/Specifications/RssSync/MonitoredMagazineIssueSpecification.cs`**
- Parallel to `MonitoredBookSpecification`
- Top guard: `if (subject is not RemoteMagazineIssue) return Decision.Accept();`
- Main logic: reject if `Issue.Monitored == false` and search is not interactive

**Required before writing these:** Do the B-0a spike. Read every spec file under `src/NzbDrone.Core/DecisionEngine/Specifications/`. For any spec that accesses `subject.Author`, `subject.Books`, or `subject.ParsedBookInfo` without a null guard, add `if (subject is RemoteMagazineIssue) return Decision.Accept();` as the first line. Document the finding in `AGENT_LOG.md` as a `DECISION` entry. If more than 3 specs need changes, build an isolated `MagazineDownloadDecisionMaker` instead of patching the book specs.

**Tests to add** (`src/NzbDrone.Core.Test/Magazines/`):
- `MonitoredMagazineIssueSpecificationFixture.cs` — unmonitored rejected during RSS, accepted during interactive search, pass-through for book subjects
- `MagazineParsingServiceFixture.cs` — match by year/month/day, null magazine returns null

---

### Gap 2 — Automatic Disk Scan Pipeline Missing (BLI014 core)

A user can add a magazine through the UI and manually import specific files via the API, but the system cannot automatically scan a `/magazines` root folder on startup or on a rescan command. This is the core feature gap.

**`src/NzbDrone.Core/Magazines/Parser/MagazineFilenameParser.cs`**

Interface:
```csharp
public interface IMagazineFilenameParser
{
    ParsedMagazineIssueInfo ParseFilename(string filename, string magazineFolderName);
    ParsedMagazineIssueInfo ParseFolderName(string folderName);
}
```

`ParseFilename` requirements:
1. Strip extension.
2. Find separator with regex `[\s]*(?:\x2D|\x2013|\x2014)[\s]*` (hyphen-minus, en dash U+2013, em dash U+2014) — match first occurrence.
3. Right of separator: parse date with `(\d{4})-(\d{1,2})(?:-(\d{1,2}))?`
4. Fallback if no separator or parse fails: `(\d{4})[\W_](\d{2})` anywhere in filename.
5. Set `MagazineTitle = magazineFolderName`, `Quality` from `MagazineFormatDetector.DetectQuality(filename)`.
6. Confidence: 1.0 (YYYY-MM-DD), 0.5 (YYYY-MM), 0.0 (no date).

`ParseFolderName`: return `ParsedMagazineIssueInfo` with only `MagazineTitle = folderName`, `Confidence = 0`.

**`src/NzbDrone.Core/Magazines/MediaFiles/LocalMagazineIssue.cs`** (model only):
```csharp
public class LocalMagazineIssue
{
    public Magazine Magazine { get; set; }
    public MagazineIssue Issue { get; set; }    // null if new
    public ParsedMagazineIssueInfo ParsedInfo { get; set; }
    public IFileInfo Path { get; set; }
    public QualityModel Quality { get; set; }
}
```

**`src/NzbDrone.Core/Magazines/MediaFiles/MagazineImportDecision.cs`** (model only):
```csharp
public class MagazineImportDecision
{
    public LocalMagazineIssue LocalIssue { get; set; }
    public List<Rejection> Rejections { get; set; }
    public bool Approved => Rejections.Count == 0;
}
```

**`src/NzbDrone.Core/Magazines/MediaFiles/MagazineImportDecisionMaker.cs`**

Interface: `IMagazineImportDecisionMaker`
- `GetImportDecisions(List<IFileInfo> files, Magazine magazine, ParsedMagazineIssueInfo folderInfo)`

Rejection reasons:
- `ParseFailed` — confidence == 0
- `AlreadyImported` — path already in `MagazineIssueFiles`
- `ExistingFile` — issue has a file of equal or better quality

**`src/NzbDrone.Core/Magazines/MediaFiles/ImportApprovedMagazineIssues.cs`**

Interface: `IImportApprovedMagazineIssues`
- `Import(List<MagazineImportDecision> decisions, bool newDownload)`

For each approved decision: upsert `MagazineIssue`, create `MagazineIssueFile`, emit `MagazineIssueFileImportedEvent`.

**`src/NzbDrone.Core/Magazines/MediaFiles/MagazineDiskScanService.cs`**

Interface: `IMagazineDiskScanService`
- `Scan(List<string> folders = null)`
- `GetMagazineFiles(string path, bool allDirectories = true)`

Implementation flow:
1. Load all `MagazineRootFolder` paths from `IMagazineRootFolderService`.
2. For each root: list immediate subdirectories (each = one magazine title).
3. For each subdirectory:
   a. `IMagazineFilenameParser.ParseFolderName(folderName)` → raw title.
   b. `IMagazineService.FindByNormalizedTitle(NormalizeTitle(rawTitle))` → magazine.
   c. If not found and auto-add enabled: call `IAddMagazineService.AddMagazine` (non-blocking on Wikidata).
   d. List files in subdirectory.
   e. For each file: `ParseFilename(filename, folderName)`.
   f. Skip confidence == 0 with a warning log.
   g. Skip paths already in `MagazineIssueFiles`.
   h. Call `IMagazineImportDecisionMaker.GetImportDecisions`.
4. Call `IImportApprovedMagazineIssues.Import` for approved decisions.
5. Emit `MagazineScanCompleteEvent`.
6. Wire as handler for `RescanMagazineCommand` (command already exists).

**Tests to add** (`src/NzbDrone.Core.Test/Magazines/`):
- `MagazineFilenameParserFixture.cs`:
  - Hyphen-minus separator: `"Motor Trend - 2024-03-01.pdf"` → year=2024, month=3, day=1
  - En dash separator: `"PC World – 2023-11.cbz"` → year=2023, month=11, day=null
  - Em dash separator: `"Car and Driver — 2022-06-01.epub"` → year=2022, month=6
  - Fallback regex: `"motortrend_202403.pdf"` → year=2024, month=3
  - No date: confidence == 0
  - `ParseFolderName`: returns title only
- `MagazineImportDecisionMakerFixture.cs`:
  - Skips already-imported files
  - Rejects confidence-0 parses
  - Approves new issue

---

### Gap 3 — Frontend Index, Detail, and Navigation Missing (Stream F residual)

After adding a magazine, the user has no page to browse to. The Redux store, reducers, index page, detail page, and navigation are all absent.

**Redux store** (mirror `frontend/src/Author/authorActions.js` pattern):

- `frontend/src/Magazine/magazineActions.js` — FETCH/SET/SAVE/DELETE/TOGGLE_MAGAZINE_MONITORED, API base `/api/v1/magazine`
- `frontend/src/Magazine/magazineIssueActions.js` — FETCH/SET/SET_MAGAZINE_ISSUE_MONITORED, API base `/api/v1/magazineissue`
- `frontend/src/Magazine/magazineIndexActions.js` — pagination/sort/filter for the list
- `frontend/src/Store/Actions/magazineRootFolderActions.js` — mirrors `rootFolders.js`, API base `/api/v1/magazinerootfolder`
- `frontend/src/Store/Reducers/magazines.js` — handle FETCH/SET/SAVE/DELETE
- `frontend/src/Store/Reducers/magazineIssues.js` — handle FETCH/SET/MONITORED changes
- Register both reducers in the root `combineReducers` call (find it in `frontend/src/Store/`)

**Index** (mirror `frontend/src/Author/Index/`):
- `frontend/src/Magazine/Index/MagazineIndex.js`
- `frontend/src/Magazine/Index/MagazineIndexItem.js` — title, monitored badge, issue count
- `frontend/src/Magazine/Index/MagazineIndexConnector.js`

**Detail** (mirror `frontend/src/Author/Details/`):
- `frontend/src/Magazine/Details/MagazineDetails.js` — header with monitored toggle, edit/delete; body with issue table
- `frontend/src/Magazine/Details/MagazineDetailsConnector.js`
- `frontend/src/Magazine/Details/MagazineIssueRow.js` — date (YYYY-MM), monitored toggle, file status, search button
- `frontend/src/Magazine/Details/MagazineIssueRowConnector.js`

**Navigation and routing** (do not add to any Author/Book page):
- Find the sidebar nav component (likely `frontend/src/App/AppSidebarItem.js` or `Sidebar/`). Add a "Magazines" link pointing to `/magazine`.
- Find the router file (likely `frontend/src/App/App.js`). Add:
  - `<Route path="/magazine" component={MagazineIndex} />`
  - `<Route path="/magazine/:id" component={MagazineDetails} />`

**Verification:** `yarn lint && yarn build` must be green with no new errors. Navigate to `/magazine` in the browser — index page must render. Navigate to `/magazine/1` — detail page must render.

---

### Gap 4 — docker-compose Magazine Volume Missing (minor)

Per plan `06-api.md` Task E-7 and `08-corrections.md` Q5 resolution:

**`docker-compose.yml`:** Add alongside the existing `/books` and `/audiobooks` mounts:
```yaml
- "${MAGAZINES_DIR:-./magazines}:/magazines"
```

**`Dockerfile`:** Add `VOLUME /magazines` alongside the existing `VOLUME /books` and `VOLUME /audiobooks`.

---

## Execution Order for Codex

### Step 0 — Before touching any file
1. Read `AGENTS.md` fully.
2. Run `git status --short --branch` — must show `develop` clean.
3. Add a `START` entry to `AGENT_LOG.md` naming which gap(s) you are targeting.
4. Do the B-0a spike (audit decision specs for null safety on `RemoteMagazineIssue`). Record the finding as a `DECISION` in `AGENT_LOG.md` before writing any pipeline code.

### Step 1 — Download pipeline (Gap 1)
`RemoteMagazineIssue.cs` → `MagazineParsingService.cs` → `MonitoredMagazineIssueSpecification.cs` → null guards from B-0a spike → tests

Verify: `dotnet build src/Readarr.sln --no-restore -m:1` (0 errors); `dotnet test --filter "FullyQualifiedName~MonitoredMagazineIssueSpecification"` green.

### Step 2 — Automatic disk scan (Gap 2)
`MagazineFilenameParser.cs` → `LocalMagazineIssue.cs` + `MagazineImportDecision.cs` → `MagazineImportDecisionMaker.cs` → `ImportApprovedMagazineIssues.cs` → `MagazineDiskScanService.cs` → tests

Verify: `dotnet test --filter "FullyQualifiedName~MagazineFilenameParser|FullyQualifiedName~MagazineImportDecisionMaker"` green.

### Step 3 — Frontend index/detail/nav (Gap 3)
Redux store → reducers → root combineReducers → Index components → Detail components → nav link + route registration

Verify: `yarn lint && yarn build` green; manual browser check at `/magazine`.

### Step 4 — docker-compose (Gap 4)
Two-line change to `docker-compose.yml` and one-line change to `Dockerfile`.

---

## Acceptance Criteria Still Unmet

- [ ] `MagazineDiskScanService.Scan()` processes a `/magazines` directory and produces `MagazineIssue` + `MagazineIssueFile` DB records automatically
- [ ] `MagazineFilenameParser` handles hyphen-minus, en dash, and em dash separators
- [ ] Confidence = 1.0 for YYYY-MM-DD; 0.5 for YYYY-MM; 0.0 for no date
- [ ] `MonitoredMagazineIssueSpecification` rejects unmonitored issues during RSS sync
- [ ] Navigating to `/magazine` shows the index page (not a 404)
- [ ] Navigating to `/magazine/1` shows the detail view with the issue list
- [ ] Magazine sidebar nav link is visible without modifying any Author/Book page
- [ ] `dotnet build src/Readarr.sln` green; `yarn lint && yarn build` green

## Do Not Touch

- Any existing Author/Book/Series code paths beyond null guards for `RemoteMagazineIssue`
- `DiskScanService.cs` — book scan is NOT modified; magazine scan is a separate service
- BLI001–BLI011 completed work
