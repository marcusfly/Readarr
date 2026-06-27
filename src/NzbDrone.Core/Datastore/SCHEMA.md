# Readarr Database Schema

Canonical reference for the Readarr main database schema as produced by running
the full current migration set against a fresh database.

Supported backends: **SQLite** (default) and **PostgreSQL**.

All tables include an auto-increment `Id` column (INTEGER PRIMARY KEY in SQLite;
SERIAL PRIMARY KEY in PostgreSQL) unless otherwise noted.

---

## Core Entity Tables

### Authors
Represents a monitored author in the library.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| CleanName | TEXT | NO | Indexed |
| Path | TEXT | NO | Indexed |
| AudiobookPath | TEXT | YES | Separate audiobook author path/root |
| Monitored | INTEGER | NO | Boolean |
| LastInfoSync | DATETIME | YES | |
| SortName | TEXT | YES | |
| QualityProfileId | INTEGER | YES | FK → QualityProfiles.Id |
| Tags | TEXT | YES | JSON array |
| Added | DATETIME | YES | |
| AddOptions | TEXT | YES | JSON |
| MetadataProfileId | INTEGER | NO | FK → MetadataProfiles.Id; default 1 |
| AuthorMetadataId | INTEGER | NO | UNIQUE; FK → AuthorMetadata.Id |

Indexes: `CleanName`, `Path`, `Monitored`, `AuthorMetadataId`

---

### AuthorMetadata
Provider-agnostic metadata for an author (name, images, links, ratings).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| ForeignAuthorId | TEXT | NO | UNIQUE; Open Library author key |
| TitleSlug | TEXT | NO | UNIQUE |
| Name | TEXT | NO | |
| Overview | TEXT | YES | |
| Disambiguation | TEXT | YES | |
| Gender | TEXT | YES | |
| Hometown | TEXT | YES | |
| Born | DATETIME | YES | |
| Died | DATETIME | YES | |
| Status | INTEGER | NO | |
| Images | TEXT | NO | JSON array |
| Links | TEXT | YES | JSON array |
| Genres | TEXT | YES | JSON array |
| Ratings | TEXT | YES | JSON |
| Aliases | TEXT | NO | JSON array; default `[]` |

---

### Books
A logical book entity (edition-agnostic).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| AuthorMetadataId | INTEGER | NO | Indexed; FK → AuthorMetadata.Id |
| ForeignBookId | TEXT | NO | Indexed; Open Library work key |
| TitleSlug | TEXT | NO | UNIQUE |
| Title | TEXT | NO | |
| ReleaseDate | DATETIME | YES | |
| Links | TEXT | YES | JSON array |
| Genres | TEXT | YES | JSON array |
| Ratings | TEXT | YES | JSON |
| CleanTitle | TEXT | NO | Indexed |
| Monitored | INTEGER | NO | Boolean |
| AnyEditionOk | INTEGER | NO | Boolean |
| LastInfoSync | DATETIME | YES | |
| Added | DATETIME | YES | |
| AddOptions | TEXT | YES | JSON |

Indexes: `AuthorMetadataId`, `AuthorMetadataId + ReleaseDate`, `CleanTitle`, `ForeignBookId`

---

### Editions
A specific published edition of a book.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| BookId | INTEGER | NO | FK → Books.Id |
| ForeignEditionId | TEXT | NO | UNIQUE; Open Library edition key |
| Isbn13 | TEXT | YES | |
| Asin | TEXT | YES | |
| Title | TEXT | NO | |
| TitleSlug | TEXT | NO | |
| Language | TEXT | YES | |
| Overview | TEXT | YES | |
| Format | TEXT | YES | |
| IsEbook | INTEGER | YES | Boolean |
| Disambiguation | TEXT | YES | |
| Publisher | TEXT | YES | |
| PageCount | INTEGER | YES | |
| ReleaseDate | DATETIME | YES | |
| Images | TEXT | NO | JSON array |
| Links | TEXT | YES | JSON array |
| Ratings | TEXT | YES | JSON |
| Monitored | INTEGER | NO | Boolean |
| ManualAdd | INTEGER | NO | Boolean |

Indexes: `Monitored` (via migration 022)

---

### BookFiles
A physical file imported into the library.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| EditionId | INTEGER | NO | Indexed; FK → Editions.Id |
| CalibreId | INTEGER | NO | |
| Quality | TEXT | NO | JSON |
| Size | INTEGER | NO | |
| SceneName | TEXT | YES | |
| DateAdded | DATETIME | NO | |
| ReleaseGroup | TEXT | YES | |
| MediaInfo | TEXT | YES | JSON |
| Modified | DATETIME | NO | Default 2000-01-01 |
| Path | TEXT | NO | UNIQUE |
| Part | INTEGER | NO | Added migration 010; default 1 |
| OriginalFilePath | TEXT | YES | Added migration 026 |
| IndexerFlags | INTEGER | NO | Added migration 040; default 0 |

---

### Series
A book series grouping.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| ForeignSeriesId | TEXT | NO | UNIQUE |
| Title | TEXT | NO | |
| Description | TEXT | YES | |
| Numbered | INTEGER | NO | Boolean |
| WorkCount | INTEGER | NO | |
| PrimaryWorkCount | INTEGER | NO | |

---

### SeriesBookLink
Join table linking books to series.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| SeriesId | INTEGER | NO | Indexed; FK → Series.Id CASCADE DELETE |
| BookId | INTEGER | NO | FK → Books.Id CASCADE DELETE |
| Position | TEXT | YES | |
| IsPrimary | INTEGER | NO | Boolean |

---

### Magazines
A monitored publication title tracked by Readarr.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| CleanTitle | TEXT | NO | Indexed |
| Title | TEXT | NO | |
| NormalizedTitle | TEXT | NO | |
| Aliases | TEXT | NO | JSON array; default `[]` |
| Issn | TEXT | YES | |
| IssnL | TEXT | YES | |
| WikidataId | TEXT | YES | |
| Publisher | TEXT | YES | |
| Country | TEXT | YES | |
| Language | TEXT | YES | |
| Monitored | INTEGER | NO | Boolean |
| Path | TEXT | YES | Indexed |
| RootFolderPath | TEXT | YES | |
| QualityProfileId | INTEGER | NO | Default 1 |
| MetadataProfileId | INTEGER | NO | Default 1 |
| Tags | TEXT | YES | JSON array |
| Added | DATETIME | YES | |
| LastInfoSync | DATETIME | YES | |
| AddOptions | TEXT | YES | JSON |

---

### MagazineIssues
Tracked publication issues/editions within a magazine.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| MagazineId | INTEGER | NO | Indexed; FK → Magazines.Id |
| IssueYear | INTEGER | NO | |
| IssueMonth | INTEGER | NO | |
| IssueDay | INTEGER | YES | |
| Volume | TEXT | YES | |
| IssueNumber | TEXT | YES | |
| ReleaseTitle | TEXT | YES | |
| Monitored | INTEGER | NO | Boolean |
| Added | DATETIME | YES | |
| LastSearchTime | DATETIME | YES | |

Indexes: `MagazineId`, `MagazineId + IssueYear + IssueMonth + IssueDay` (UNIQUE)

---

### MagazineIssueFiles
Physical files for specific magazine issues.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| MagazineIssueId | INTEGER | NO | Indexed; FK → MagazineIssues.Id |
| MagazineId | INTEGER | NO | Indexed; FK → Magazines.Id |
| Path | TEXT | NO | UNIQUE |
| Size | INTEGER | NO | Default 0 |
| DateAdded | DATETIME | YES | |
| Quality | TEXT | YES | JSON |
| MediaInfo | TEXT | YES | JSON |

---

### MagazineRootFolders
Magazine library roots for collection configuration.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Name | TEXT | YES | |
| Path | TEXT | NO | UNIQUE |
| DefaultQualityProfileId | INTEGER | NO | Default 1 |
| DefaultMetadataProfileId | INTEGER | NO | Default 1 |
| DefaultMonitorOption | INTEGER | NO | Default 0 |
| DefaultTags | TEXT | YES | JSON array |

---

## Configuration Tables

### Config
Key-value application configuration.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Key | TEXT | NO | UNIQUE |
| Value | TEXT | NO | |

---

### RootFolders
Library root paths watched by Readarr.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Path | TEXT | NO | UNIQUE |
| Name | TEXT | YES | |
| DefaultMetadataProfileId | INTEGER | NO | Default 0 |
| DefaultQualityProfileId | INTEGER | NO | Default 0 |
| DefaultMonitorOption | INTEGER | NO | Default 0 |
| DefaultTags | TEXT | YES | JSON array |
| IsCalibreLibrary | INTEGER | NO | Boolean |
| CalibreSettings | TEXT | YES | JSON |

---

### NamingConfig
File and folder naming format strings.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| ReplaceIllegalCharacters | INTEGER | NO | Boolean; default true |
| AuthorFolderFormat | TEXT | YES | |
| RenameBooks | INTEGER | YES | Boolean |
| StandardBookFormat | TEXT | YES | |
| ColonReplacementFormat | INTEGER | NO | Added migration 031; default 4 |

---

### QualityProfiles
Download quality profiles.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Name | TEXT | NO | UNIQUE |
| Cutoff | INTEGER | NO | |
| Items | TEXT | NO | JSON array |
| UpgradeAllowed | INTEGER | YES | Boolean |
| FormatItems | TEXT | NO | Added migration 026; default `[]` |
| MinFormatScore | INTEGER | NO | Added migration 026; default 0 |
| CutoffFormatScore | INTEGER | NO | Added migration 026; default 0 |

---

### MetadataProfiles
Metadata filtering profiles.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Name | TEXT | NO | UNIQUE |
| MinPopularity | REAL | NO | |
| SkipMissingDate | INTEGER | NO | Boolean |
| SkipMissingIsbn | INTEGER | NO | Boolean |
| SkipPartsAndSets | INTEGER | NO | Boolean |
| SkipSeriesSecondary | INTEGER | NO | Boolean |
| AllowedLanguages | TEXT | YES | |
| MinPages | INTEGER | NO | Added migration 008; default 0 |
| Ignored | TEXT | YES | Added migration 033; JSON array |

---

### QualityDefinitions
Human-readable size limits per quality level.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Quality | INTEGER | NO | UNIQUE |
| Title | TEXT | NO | UNIQUE |
| MinSize | REAL | YES | |
| MaxSize | REAL | YES | |

---

### DelayProfiles
Protocol delay preferences.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| EnableUsenet | INTEGER | NO | Boolean |
| EnableTorrent | INTEGER | NO | Boolean |
| PreferredProtocol | INTEGER | NO | |
| UsenetDelay | INTEGER | NO | |
| TorrentDelay | INTEGER | NO | |
| Order | INTEGER | NO | |
| Tags | TEXT | NO | JSON array |
| BypassIfHighestQuality | INTEGER | NO | Added migration 026; default false |
| BypassIfAboveCustomFormatScore | INTEGER | NO | Added migration 026; default false |
| MinimumCustomFormatScore | INTEGER | YES | Added migration 026 |

---

### CustomFormats
User-defined release format scoring rules.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Name | TEXT | NO | UNIQUE |
| Specifications | TEXT | NO | JSON array; default `[]` |
| IncludeCustomFormatWhenRenaming | INTEGER | NO | Boolean; default false |

---

### Tags
Free-form string labels applied to many entity types.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Label | TEXT | NO | UNIQUE |

---

### RemotePathMappings
Maps download-client remote paths to local paths.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Host | TEXT | NO | |
| RemotePath | TEXT | NO | |
| LocalPath | TEXT | NO | |

---

### CustomFilters
Saved filter presets for the UI.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Type | TEXT | NO | |
| Label | TEXT | NO | |
| Filters | TEXT | NO | JSON array |

---

## Download & History Tables

### History
Per-book-file import/grab/failure events.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| SourceTitle | TEXT | NO | |
| Date | DATETIME | NO | Indexed |
| Quality | TEXT | NO | JSON |
| Data | TEXT | NO | JSON |
| EventType | INTEGER | YES | Indexed |
| DownloadId | TEXT | YES | Indexed |
| AuthorId | INTEGER | NO | Default 0 |
| BookId | INTEGER | NO | Indexed; default 0 |

Composite index: `BookId + Date DESC`, `DownloadId + Date DESC`

---

### DownloadHistory
Lifecycle events for a download ID (grabbed → imported / failed).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| EventType | INTEGER | NO | |
| AuthorId | INTEGER | NO | |
| DownloadId | TEXT | NO | |
| SourceTitle | TEXT | NO | |
| Date | DATETIME | NO | |
| Protocol | INTEGER | YES | |
| IndexerId | INTEGER | YES | |
| DownloadClientId | INTEGER | YES | |
| Release | TEXT | YES | JSON |
| Data | TEXT | YES | JSON |
| BookId | INTEGER | NO | Added migration 020; default 0 |

Indexes: `EventType`, `AuthorId`, `DownloadId`

---

### Blocklist
(formerly Blacklist — renamed migration 014)  
Records of releases that should never be re-grabbed.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| SourceTitle | TEXT | NO | |
| Quality | TEXT | NO | JSON |
| Date | DATETIME | NO | |
| PublishedDate | DATETIME | YES | |
| Size | INTEGER | YES | |
| Protocol | INTEGER | YES | |
| Indexer | TEXT | YES | |
| Message | TEXT | YES | |
| TorrentInfoHash | TEXT | YES | |
| AuthorId | INTEGER | NO | Default 0 |
| BookIds | TEXT | NO | JSON array; default `""` |
| IndexerFlags | INTEGER | NO | Added migration 040; default 0 |

---

### PendingReleases
Delayed or held releases awaiting the delay profile window.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Title | TEXT | NO | |
| Added | DATETIME | NO | |
| Release | TEXT | NO | JSON |
| AuthorId | INTEGER | NO | Default 0 |
| ParsedBookInfo | TEXT | NO | JSON; default `""` |
| Reason | INTEGER | NO | Default 0 |

---

## Infrastructure Tables

### DownloadClients
Configured download client adapters.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Enable | INTEGER | NO | Boolean |
| Name | TEXT | NO | |
| Implementation | TEXT | NO | |
| Settings | TEXT | NO | JSON |
| ConfigContract | TEXT | NO | |
| Priority | INTEGER | NO | Default 1 |
| Tags | TEXT | YES | Added migration 035; JSON array |
| RemoveCompletedDownloads | INTEGER | NO | Added migration 158; default true |
| RemoveFailedDownloads | INTEGER | NO | Added migration 158; default true |

---

### DownloadClientStatus
Circuit-breaker state for download client connectivity.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| ProviderId | INTEGER | NO | UNIQUE |
| InitialFailure | DATETIME | YES | |
| MostRecentFailure | DATETIME | YES | |
| EscalationLevel | INTEGER | NO | |
| DisabledTill | DATETIME | YES | |

---

### Indexers
Configured indexer adapters.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Name | TEXT | NO | UNIQUE |
| Implementation | TEXT | NO | |
| Settings | TEXT | YES | JSON |
| ConfigContract | TEXT | YES | |
| EnableRss | INTEGER | YES | Boolean |
| EnableAutomaticSearch | INTEGER | YES | Boolean |
| EnableInteractiveSearch | INTEGER | NO | Boolean |
| Priority | INTEGER | NO | Added migration 003; default 25 |
| Tags | TEXT | YES | Added migration 028; JSON array |
| DownloadClientId | INTEGER | NO | Added migration 030; default 0 |

---

### IndexerStatus
Circuit-breaker state for indexer connectivity.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| ProviderId | INTEGER | NO | UNIQUE |
| InitialFailure | DATETIME | YES | |
| MostRecentFailure | DATETIME | YES | |
| EscalationLevel | INTEGER | NO | |
| DisabledTill | DATETIME | YES | |
| LastRssSyncReleaseInfo | TEXT | YES | |

---

### Notifications
Configured notification adapters.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Name | TEXT | NO | |
| OnGrab | INTEGER | NO | Boolean |
| Settings | TEXT | NO | JSON |
| Implementation | TEXT | NO | |
| ConfigContract | TEXT | YES | |
| OnUpgrade | INTEGER | YES | Boolean |
| Tags | TEXT | YES | JSON array |
| OnRename | INTEGER | NO | Boolean |
| OnReleaseImport | INTEGER | NO | Boolean; default false |
| OnHealthIssue | INTEGER | NO | Boolean; default false |
| IncludeHealthWarnings | INTEGER | NO | Boolean; default false |
| OnDownloadFailure | INTEGER | NO | Boolean; default false |
| OnImportFailure | INTEGER | NO | Boolean; default false |
| OnTrackRetag | INTEGER | NO | Boolean; default false |
| OnDelete | INTEGER | NO | Added migration 021; default false |
| OnUpdate | INTEGER | NO | Added migration 025; default false |
| OnAuthorAdded | INTEGER | NO | Added migration 038; default false |

---

### NotificationStatus
Circuit-breaker state for notification delivery.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| ProviderId | INTEGER | NO | UNIQUE |
| InitialFailure | DATETIME | YES | |
| MostRecentFailure | DATETIME | YES | |
| EscalationLevel | INTEGER | NO | |
| DisabledTill | DATETIME | YES | |

Added by migration 037.

---

### Metadata
Configured metadata consumers (e.g. Kodi/Emby scraper files).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Enable | INTEGER | NO | Boolean |
| Name | TEXT | NO | |
| Implementation | TEXT | NO | |
| Settings | TEXT | NO | JSON |
| ConfigContract | TEXT | NO | |

---

### MetadataFiles
Files written by metadata consumers.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| AuthorId | INTEGER | NO | |
| Consumer | TEXT | NO | |
| Type | INTEGER | NO | |
| RelativePath | TEXT | NO | |
| LastUpdated | DATETIME | NO | |
| BookId | INTEGER | YES | |
| BookFileId | INTEGER | YES | |
| Hash | TEXT | YES | |
| Added | DATETIME | YES | |
| Extension | TEXT | NO | |

---

### ExtraFiles
Non-book files in the library (subtitles, NFO files, etc.).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| AuthorId | INTEGER | NO | |
| BookId | INTEGER | NO | |
| BookFileId | INTEGER | NO | |
| RelativePath | TEXT | NO | |
| Extension | TEXT | NO | |
| Added | DATETIME | NO | |
| LastUpdated | DATETIME | NO | |

---

### ScheduledTasks
Recurring background job registry.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| TypeName | TEXT | NO | UNIQUE |
| Interval | INTEGER | NO | Minutes |
| LastExecution | DATETIME | NO | |
| LastStartTime | DATETIME | YES | |

---

### Commands
Command queue / audit log.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Name | TEXT | NO | |
| Body | TEXT | NO | JSON |
| Priority | INTEGER | NO | |
| Status | INTEGER | NO | |
| QueuedAt | DATETIME | NO | |
| StartedAt | DATETIME | YES | |
| EndedAt | DATETIME | YES | |
| Duration | TEXT | YES | |
| Exception | TEXT | YES | |
| Trigger | INTEGER | NO | |
| Result | INTEGER | NO | Added migration 036; default 1 |

---

### ImportAttempts
Durable journal for crash-safe file imports.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| SourcePath | TEXT | NO | Indexed |
| DestinationPath | TEXT | YES | |
| Status | INTEGER | NO | Indexed; default pending |
| StartedAt | DATETIME | NO | |
| FinishedAt | DATETIME | YES | |
| IsDryRun | INTEGER | NO | Boolean; default false |
| ErrorMessage | TEXT | YES | |

Added by migration 042.

---

### Users
Local authentication accounts.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Identifier | TEXT | NO | UNIQUE |
| Username | TEXT | NO | UNIQUE |
| Password | TEXT | NO | |

---

## Import List Tables

### ImportLists
Configured import list sources.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Name | TEXT | NO | UNIQUE |
| Implementation | TEXT | NO | |
| Settings | TEXT | YES | JSON |
| ConfigContract | TEXT | YES | |
| EnableAutomaticAdd | INTEGER | YES | Boolean |
| RootFolderPath | TEXT | NO | |
| ShouldMonitor | INTEGER | NO | |
| ProfileId | INTEGER | NO | |
| MetadataProfileId | INTEGER | NO | |
| Tags | TEXT | YES | JSON array |
| SearchOnAdd | INTEGER | NO | Added migration 002 |
| MonitorNewItems | INTEGER | NO | Added migration 019 |
| ListOrder | INTEGER | NO | Added migration 029; default 0 |

---

### ImportListStatus
Circuit-breaker state for import list sources.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| ProviderId | INTEGER | NO | UNIQUE |
| InitialFailure | DATETIME | YES | |
| MostRecentFailure | DATETIME | YES | |
| EscalationLevel | INTEGER | NO | |
| DisabledTill | DATETIME | YES | |
| LastSyncListInfo | TEXT | YES | |

---

### ImportListExclusions
Authors/books to permanently exclude from import lists.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| ForeignId | TEXT | NO | UNIQUE |
| Name | TEXT | NO | |

---

## Profile Tables

### ReleaseProfiles
Preferred/ignored term rules (Usenet).

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Required | TEXT | YES | JSON array |
| Ignored | TEXT | YES | JSON array |
| Tags | TEXT | NO | JSON array |
| Enabled | INTEGER | NO | Added migration 005; default true |
| IndexerId | INTEGER | NO | Added migration 005; default 0 |

Note: `Preferred` and `IncludePreferredWhenRenaming` columns were removed by migration 026.

---

## Update & Audit Tables

### UpdateHistory
Log of Readarr version updates.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Date | DATETIME | NO | |
| Version | TEXT | NO | |
| EventType | INTEGER | NO | |

Added by migration 024.

---

## HTTP Cache Table (Cache Database)

### HttpResponse
Cached HTTP responses used by metadata providers.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Url | TEXT | NO | Indexed |
| LastRefresh | DATETIME | NO | |
| Expiry | DATETIME | NO | Indexed |
| Value | TEXT | NO | |
| StatusCode | INTEGER | NO | |

---

## Log Table (Log Database)

### Logs
Application log entries.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | INTEGER | NO | PK |
| Message | TEXT | NO | |
| Time | DATETIME | NO | Indexed |
| Logger | TEXT | NO | |
| Exception | TEXT | YES | |
| ExceptionType | TEXT | YES | |
| Level | TEXT | NO | |

---

## Migration Version Table (internal)

### VersionInfo (FluentMigrator managed)
Tracks which migrations have been applied.

| Column | Type | Notes |
|--------|------|-------|
| Version | INTEGER | Migration number |
| AppliedOn | DATETIME | UTC timestamp |
| Description | TEXT | Migration class name |

---

## Notes

- All boolean columns are stored as `INTEGER` (0/1) in SQLite and `BOOLEAN` in PostgreSQL.
- All `DATETIME` columns use `TEXT` (ISO 8601) in SQLite and `TIMESTAMPTZ` in PostgreSQL (enforced by migrations 023 and 032).
- JSON columns store serialised objects/arrays as `TEXT`.
- The `Id` column on every model table is the surrogate primary key managed by Dapper/ServiceStack.OrmLite.
- Foreign key constraints exist in the schema definition (FluentMigrator `ForeignKey()` calls) but SQLite only enforces them when `PRAGMA foreign_keys = ON`.
