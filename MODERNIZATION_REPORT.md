# Readarr Non-.NET Modernization Assessment

**Assessment date:** June 12, 2026  
**Repository branch:** `develop-mfly`  
**Decision horizon:** 3-5 years  
**Estimate confidence:** Rough order of magnitude, approximately -25% / +50%

## Executive conclusion

A full, feature-compatible rewrite of Readarr without .NET is technically possible,
but it is **not economically justified as the first move**.

The principal reason Readarr was retired was the failure of its metadata product
dependency, not a limitation of C# or ASP.NET. Rewriting approximately 102,000 lines
of production backend code would not fix metadata quality, identifier stability, or
community maintenance. It would add a multi-year regression program before users
receive meaningful new capability.

If eliminating .NET is mandatory, the recommended target is:

- **Go modular monolith** for the API, scheduler, acquisition engine, integrations,
  filesystem operations, migrations, and packaging.
- **React plus TypeScript** for the web client, migrated incrementally rather than
  rewritten at the same time.
- **SQLite first**, preserving zero-configuration self-hosting; add PostgreSQL after
  the core workflow is stable.
- **Docker/Linux first**, with Windows and macOS native packaging later.
- **External metadata service boundary**, initially compatible with
  rreading-glasses and capable of adding Hardcover or Open Library adapters.
- **REST API v1 compatibility** where practical, but replace the internal SignalR
  transport with a documented WebSocket or Server-Sent Events protocol.

The sensible investment is a **6-8 week validation phase**, followed by a reduced
scope Go reboot only if the product and contributor model pass explicit gates. Do not
approve a full parity rewrite up front.

## Current-state evidence

The application is a mature automation system, not just a CRUD website.

| Measure | Repository evidence |
|---|---:|
| C# files | 2,148 |
| C# lines, including tests | 142,311 |
| Estimated production C# lines | 101,863 |
| Backend test lines | 40,448 |
| Files containing backend tests | 353 |
| Test and test-case declarations | 3,285 |
| API controllers | 70 |
| HTTP/custom REST action declarations | At least 147 |
| Persistent mapped entities | 41 |
| Database migration files | 50 |
| Command-related files | 78 |
| Domain event types | 58 |
| Frontend JS/TS files | 1,402 |
| Frontend JS/TS lines | 89,864 |
| Legacy JavaScript files | 990 |
| TypeScript files | 412 |
| CSS files / lines | 361 / 8,098 |
| Target runtime packages | 10 Windows, macOS, glibc, musl, x64, x86, ARM variants |

The largest backend areas are download handling, notifications, media files,
datastore behavior, books, indexers, import lists, metadata, and release decision
logic. These areas contain the product's accumulated edge-case behavior.

### Runtime and application responsibilities

The replacement must account for all of the following:

1. HTTP API, OpenAPI generation, authentication, CORS, reverse-proxy headers, URL
   base support, TLS, static UI serving, and response middleware.
2. API-key, basic, forms/cookie, no-auth, and external-auth modes.
3. Realtime command, queue, health, author, book, file, and settings updates.
4. Durable scheduled tasks and a three-worker command queue.
5. In-process synchronous and asynchronous domain event dispatch.
6. SQLite and PostgreSQL, three logical databases, custom serialization, lazy
   loading, dialect-specific queries, backup, restore, corruption handling, and
   migrations.
7. Release parsing, quality profiles, custom formats, ranking, rejection rules,
   upgrades, delay profiles, and failed-download handling.
8. Newznab/Torznab and specialist indexer behavior.
9. At least 15 download-client families and their version-specific protocols.
10. At least 24 notification families plus custom scripts and webhooks.
11. Import lists, Calibre integration, metadata lookup, metadata refresh, cover
    handling, and identifiers inherited from Goodreads.
12. Library scans, renaming, copy/move/hardlink behavior, permissions, remote path
    mapping, free-space checks, and filesystem watch coordination.
13. Ebook, audiobook, image, PDF, archive, torrent, and tag parsing.
14. Logging, log database retention, health checks, diagnostics, Sentry, and
    analytics behavior.
15. Backup/restore, built-in updater, external updater detection, process restart,
    service management, single-instance enforcement, Windows tray and firewall
    behavior, macOS app packaging, and native installers.
16. Existing API clients, webhooks, scripts, database backups, configuration files,
    and user library layouts.

## The real modernization problem

### 1. The backend runtime is unsupported

The repository targets .NET 6. Microsoft ended .NET 6 support on
**November 12, 2024**. Running it indefinitely creates security and platform support
risk. This is urgent, but replacing the language is not the only way to resolve it.

### 2. Metadata is the product-critical blocker

The repository's June 27, 2025 retirement announcement says metadata had become
unusable and the Open Library transition had stalled. The current domain model,
search syntax, webhooks, imports, and refresh logic are deeply coupled to Goodreads
IDs and to the old Readarr metadata API contract.

A replacement needs a metadata strategy before it needs a new web framework:

- Stable, namespaced IDs for authors, works, editions, ISBNs, ASINs, and provider
  records.
- Deterministic merge and de-duplication rules.
- Edition selection and language rules.
- Incremental refresh and deletion semantics.
- Search quality targets and representative acceptance datasets.
- Cover-image sourcing and caching rules.
- Rate limiting, retries, local caching, and provider outage behavior.
- Migration rules for existing Goodreads-derived IDs.
- Clear terms-of-service and redistribution review for every metadata source.

Open Library offers APIs and monthly bulk dumps, but its data model and quality cannot
be assumed to match Readarr's author/work/edition expectations. Its current dump is
large enough that a self-hosted local metadata index is a separate product and
operations project. The community rreading-glasses service is an immediately useful
compatibility layer, but it is still an external community dependency and should not
become an undocumented single point of failure.

### 3. Most risk is hidden behavior

The 3,285 test declarations are valuable, but they are written against .NET
implementations and mocks. A rewrite cannot directly reuse them. The highest-risk
logic is integration behavior learned over years: download client quirks, path
handling, parser cases, timing, retries, import idempotency, and recovery after partial
failure.

### 4. The frontend also needs modernization

The client is React 17, React Router 5, Redux, Webpack 5, and mostly JavaScript.
Approximately 71% of JS/TS source files are still JavaScript. Rewriting the backend
while leaving the client untouched preserves substantial maintenance debt; rewriting
both simultaneously sharply increases delivery risk.

## Target stack comparison

Scores are relative to this application, from 1 (poor) to 5 (strong).

| Criterion | Go | TypeScript/Node | Rust | Python |
|---|---:|---:|---:|---:|
| Cross-platform single-process deployment | 5 | 3 | 5 | 2 |
| Long-running jobs and concurrency | 5 | 4 | 5 | 3 |
| HTTP/integration development speed | 4 | 5 | 3 | 5 |
| Filesystem/process tooling | 5 | 4 | 5 | 4 |
| SQLite/PostgreSQL support | 4 | 5 | 4 | 5 |
| Media and ebook library breadth | 3 | 4 | 3 | 4 |
| Static safety and refactorability | 4 | 4 | 5 | 2 |
| Build/release simplicity | 5 | 3 | 4 | 2 |
| Contributor accessibility | 4 | 5 | 2 | 5 |
| Fit with the existing web client | 3 | 5 | 2 | 3 |
| **Weighted result** | **4.4** | **4.2** | **3.7** | **3.4** |

### Go: recommended if .NET must be removed

Go fits a self-hosted daemon that performs HTTP integration, background work,
filesystem operations, and multi-platform distribution. It produces straightforward
native binaries, has a small runtime footprint, and makes concurrency and cancellation
explicit. The active rreading-glasses metadata compatibility project is also written
in Go, creating an opportunity to share knowledge or contracts.

Tradeoffs:

- Existing C# abstractions and tests must be translated manually.
- Some ebook, audio tag, PDF, and torrent behavior will need library evaluation or
  small focused adapters.
- Go does not reproduce the existing reflection-driven provider system naturally.
  Provider schemas should become explicit and generated.
- Supporting SQLite and PostgreSQL from day one would slow delivery.

### TypeScript/Node: credible second choice

Node would create one primary language across frontend and backend and has excellent
HTTP and database ecosystems. It is attractive for a Docker-only service.

It is less attractive for Readarr's current native distribution promise. Node 24 is
the current LTS line as of this assessment, but Node's single-executable feature is
still marked active development. Native add-ons require per-platform handling and can
complicate SQLite, media parsing, and ten-target release matrices. Node is reasonable
if the product explicitly becomes Docker-first and drops native installers.

### Rust: technically strong, delivery-poor

Rust offers excellent binaries, safety, and performance, but none of those are the
binding constraint. The rewrite is already domain-heavy; adding a smaller contributor
pool and slower integration development would increase schedule risk.

### Python: not recommended for the core daemon

Python would be productive for metadata ingestion and offline tools, but packaging,
type safety, parallel work, and desktop-style distribution are weaker fits. It remains
useful for data preparation or migration utilities.

## Recommended target architecture

Build a **modular monolith**, not microservices. One installable application is a
major part of the product's value.

### Process layout

- `readarr` daemon: API, scheduler, workers, integrations, filesystem coordination.
- Static React application served by the daemon or a reverse proxy.
- Optional separate metadata service, with a stable versioned contract.
- Optional external PostgreSQL; embedded SQLite remains the default.
- No message broker for a normal single-node installation.

### Backend modules

- `catalog`: authors, works, editions, series, identifiers, monitoring.
- `metadata`: provider interfaces, mapping, caching, refresh, merge policy.
- `acquisition`: indexers, parsing, quality, custom formats, decision engine.
- `downloads`: client adapters, queue tracking, completion and failure handling.
- `library`: scans, import, rename, hardlink/copy/move, permissions, root folders.
- `calibre`: Content Server API and conversion workflows.
- `automation`: commands, durable jobs, scheduling, cancellation, progress.
- `notifications`: webhook, script, and service adapters.
- `system`: auth, configuration, health, logs, backup, update status.
- `api`: REST v1 compatibility and realtime event gateway.

Modules should communicate through typed interfaces and a small in-process event bus.
Work that must survive restart belongs in database-backed jobs with idempotency keys,
leases, attempts, and explicit terminal states. Do not reproduce the current mixture
of threads, reflection, dynamic dispatch, and implicit service discovery.

### Persistence

1. Define a canonical SQL schema independent of an ORM.
2. Use explicit queries and generated row mapping.
3. Store provider-specific identifiers in a namespaced identifier table, not columns
   named after Goodreads.
4. Store provider configuration as versioned JSON validated by generated JSON Schema.
5. Make every migration transactional where supported and test upgrades from real
   historical backups.
6. Implement SQLite first. Add PostgreSQL only after query and migration contract
   tests are stable.
7. Keep logs out of the primary transactional schema; structured files are sufficient
   initially.

Directly opening and mutating the existing database from the new application is too
risky. Build a one-time importer that reads a copy, transforms records, reports
unmapped data, validates counts and relationships, and leaves the source untouched.

### Metadata boundary

Define a versioned provider-neutral contract:

- Search authors, works, and editions.
- Resolve by provider ID, ISBN, and ASIN.
- Fetch an author graph with pagination.
- Fetch changed records since a cursor.
- Return provenance and provider identifiers on every field-bearing entity.
- Represent redirects, merges, deletions, partial results, and provider freshness.

Ship a rreading-glasses-compatible adapter first because it can preserve existing
libraries. Add a higher-quality provider only after measuring coverage against a
fixed corpus. Open Library bulk ingestion should be a separate deployable component,
not embedded in every Readarr installation.

### API and realtime compatibility

- Capture the current OpenAPI document and representative JSON before replacement.
- Preserve `/api/v1` route, status, pagination, casing, null, date, and error behavior
  for integrations that matter.
- Publish a deprecation list for endpoints that cannot be preserved.
- Replace the frontend's SignalR-specific connector with a small transport-neutral
  event client.
- Use WebSocket for bidirectional needs or Server-Sent Events if updates remain
  server-to-client only.
- Keep API key header and query support during migration.

### Frontend

Do not rewrite the UI during backend foundation work.

1. Add API contract tests around existing screens.
2. Replace SignalR coupling with a transport abstraction.
3. Move from Webpack to a current supported Vite toolchain.
4. Upgrade React, router, and state dependencies in isolated steps.
5. Convert files to TypeScript by feature area with strict mode enabled gradually.
6. Generate API types from the new OpenAPI contract.
7. Replace Moment and jQuery usage only when the owning feature is touched.

### Packaging and updates

Start with:

- Linux amd64 and arm64 OCI images.
- Signed release artifacts and SBOMs.
- Configuration and database volumes.
- Health/readiness endpoints.
- External container update flow.

Add native Linux archives next, then Windows service and macOS launch integration.
Defer tray applications, built-in self-update, firewall changes, x86, 32-bit ARM,
musl, and FreeBSD until usage justifies each target. Every extra platform multiplies
integration and upgrade testing.

## Required workstreams

| Workstream | Deliverables |
|---|---|
| Product definition | Supported workflows, reduced MVP scope, compatibility policy, success metrics |
| Metadata | Provider contract, ID model, source evaluation, cache, refresh, migration, legal review |
| Contract capture | OpenAPI snapshot, JSON fixtures, webhook fixtures, parser corpus, database corpus |
| Backend foundation | Go project, configuration, logging, auth, API, shutdown, observability |
| Persistence | Schema, SQLite, migrations, repositories, importer, backup and recovery |
| Domain model | Authors, works, editions, series, profiles, tags, history |
| Automation | Durable commands, scheduler, retries, cancellation, progress, realtime events |
| Acquisition | Indexers, RSS, search, parsing, decision engine, custom formats |
| Downloads | Priority client adapters, tracking, completion, failure, remote paths |
| Library management | Scan, matching, manual import, rename, transfer, permissions, Calibre |
| Notifications | Webhook/script first, then priority service adapters |
| Frontend | Realtime abstraction, API type generation, dependency upgrades, TS conversion |
| Compatibility | API behavior, config import, DB import, webhook behavior, rollback |
| Distribution | Containers, binaries, signing, installers, services, updater policy |
| Quality | Unit, contract, integration, end-to-end, fault injection, platform matrix |
| Operations | Release process, vulnerability scanning, telemetry policy, support docs |
| Community | Governance, maintainers, issue triage, contribution guide, release ownership |

## Migration plan and gates

### Phase 0: prove the product, 6-8 weeks

Team: one senior engineer plus a product/domain owner.

- Run the current app or a maintained community fork against rreading-glasses and a
  second metadata source.
- Build a representative corpus of at least 500 authors, 5,000 works, large authors,
  series, multiple languages, ebooks, and audiobooks.
- Measure search success, edition correctness, refresh stability, and ID migration.
- Interview or survey actual users and identify the top five download clients,
  indexer paths, notification channels, and deployment platforms.
- Capture the current API, parser, webhook, and database behavior as executable
  fixtures.
- Produce an MVP feature contract and intentionally excluded list.

**Go gate:** metadata meets agreed coverage and correctness targets; at least three
committed maintainers or funded equivalents exist; the reduced MVP has real users.

**Stop gate:** metadata remains unreliable, there is no maintainer capacity, or users
will not accept reduced compatibility.

### Phase 1: walking skeleton, 8-12 weeks

- Go daemon, configuration, auth, SQLite, migrations, REST conventions, jobs,
  structured logs, and realtime events.
- Read-only import of existing configuration and database snapshots.
- Catalog browse/search against the chosen metadata adapter.
- React client can authenticate and render basic catalog data.
- CI builds Linux amd64/arm64 images and runs contract tests.

### Phase 2: minimum useful automation, 4-6 months

- Authors, books, monitoring, profiles, root folders, history.
- Prowlarr/Torznab/Newznab integration.
- The three most-used download clients.
- Search, RSS, decision engine, queue, completed download handling.
- Scan, manual import, rename, copy/move/hardlink.
- Webhook and custom-script notifications.
- Backup and a supported one-way migration tool.

At this point the product can begin an opt-in beta. It is not full Readarr parity.

### Phase 3: ecosystem expansion, 6-12 months

- Remaining high-value clients, indexers, import lists, and notifications.
- Calibre conversion and less common media formats.
- PostgreSQL support.
- Broader API compatibility and migration hardening.
- Performance, fault recovery, and large-library testing.
- Frontend dependency and TypeScript migration.

### Phase 4: native distribution and retirement, 3-6 months

- Windows service and installer, macOS packaging, native Linux archives.
- Signed updates or a documented external update mechanism.
- Side-by-side migration, rollback, support documentation, and final compatibility
  report.
- Retire the .NET implementation only after two stable release cycles and successful
  migrations from real user backups.

## Effort and staffing

These ranges include engineering, tests, CI, migration, documentation, and release
work. They do not include a large custom metadata ingestion platform.

| Outcome | Estimated effort | Credible team/calendar |
|---|---:|---|
| Product and metadata validation | 8-14 engineer-weeks | 1-2 people, 6-8 weeks |
| Docker-only reduced MVP | 90-150 engineer-weeks | 3-4 people, 8-12 months |
| Broad beta, common integrations | 180-280 engineer-weeks | 4-5 people, 12-20 months |
| Near parity across platforms | 280-450 engineer-weeks | 4-6 people, 18-30 months |
| Full historical parity | 400+ engineer-weeks | Multi-year, with uncertain payoff |

For budget planning only, assume a fully loaded engineering cost of
**$180,000-$240,000 per engineer-year**. Under that assumption:

- A reduced MVP is approximately **$310,000-$690,000** in engineering cost.
- A broad beta is approximately **$620,000-$1.29 million**.
- Near parity is approximately **$970,000-$2.08 million**.
- Product, QA, release, infrastructure, and schedule contingency can add 25-40%,
  putting a credible near-parity program around **$1.2-$2.9 million**.

These are scenario assumptions, not vendor quotes or market-rate forecasts.

A credible team is:

- One technical lead/architect who also codes.
- Two to four backend/integration engineers.
- One frontend engineer, initially part-time.
- One QA/release engineer or equivalent shared ownership.
- A product/domain owner who understands books, editions, indexers, download clients,
  and existing user workflows.

A single developer should assume **four to seven years** for broad parity, with
substantial maintenance arriving before the rewrite is finished. An unfunded volunteer
rewrite is unlikely to reach stable parity.

Ongoing ownership after launch is at least one to two active maintainer equivalents:
dependency updates, provider breakage, platform releases, security response,
integration drift, user support, and metadata quality do not stop after migration.

## Cutover and rollback

The old and new applications must never automate the same library and download
clients at the same time.

Use this cutover sequence:

1. Pause RSS, searches, imports, and download handling in the old application.
2. Wait for active commands to finish and record the current queue.
3. Create and verify application-data and library backups.
4. Run the new importer against a copy of the old database.
5. Validate row counts, foreign keys, monitored state, profiles, paths, tags,
   provider IDs, and sampled API responses.
6. Start the new application with all automation disabled.
7. Reconcile the download-client queue and manually classify ambiguous items.
8. Run read-only library scans and compare planned operations.
9. Enable imports, then manual search, then RSS in separate observation windows.
10. Keep the old application and original database unchanged for the rollback
    window.

Rollback means stopping the new application before restoring the old one. Files
already moved or renamed by the new application need a generated operation journal
and reverse plan; restoring only the database is not enough.

## Major risks and controls

| Risk | Impact | Control |
|---|---|---|
| Metadata quality or provider loss | Product becomes unusable | Provider-neutral IDs, two providers, corpus tests, cache/export path |
| Big-bang schedule failure | Years without user value | Vertical slices, beta after MVP, quarterly stop/go gates |
| Silent database corruption | Loss of user libraries | Read-only importer, backups, invariants, dry runs, rollback |
| Import/download regressions | Data loss or duplicate downloads | Idempotency keys, fixture replay, sandbox libraries, fault injection |
| API ecosystem breakage | Scripts and companion apps fail | Contract snapshots, compatibility suite, deprecation policy |
| Platform explosion | Release burden overwhelms team | Docker/Linux first; add platforms from measured demand |
| Integration long tail | Never-ending parity work | Usage-ranked adapters and explicit exclusions |
| Frontend plus backend rewrite | Scope and defect multiplication | Keep UI working, migrate transport and types incrementally |
| Maintainer attrition | New project repeats retirement | Governance, funded ownership, release rotation, bus-factor target |
| GPL obligations | Distribution or licensing errors | Preserve GPLv3, notices, corresponding source, dependency review |

Because this is GPLv3 software, a language translation based on the existing code is
still a modified/translated GPL work. Distributed versions must remain GPL-compatible
and provide corresponding source. This report is not legal advice; release plans
should receive license review.

## Alternatives and value

### A. Adopt an active community fork

Examples currently include Bookshelf and the Faustvii personal Readarr fork. They
already use working metadata compatibility services and preserve user familiarity.

- **Effort:** 2-8 weeks to evaluate, migrate, harden, and document.
- **Benefit:** Fastest route to a usable product.
- **Cost:** Remains on .NET and inherits much of the current architecture.
- **Best when:** The actual goal is a functioning book automation service.

### B. Modernize in place

Upgrade to a supported .NET release, update dependencies, replace fragile custom
infrastructure gradually, and modernize the React client.

- **Effort:** roughly 25-60 engineer-weeks before broader feature work.
- **Benefit:** Preserves behavior and tests; best risk-adjusted economics.
- **Cost:** Violates a strict no-.NET requirement.
- **Best when:** Technology choice is flexible and user value matters most.

As of June 12, 2026, .NET 10 is the current LTS release, supported through
November 14, 2028. The repository's .NET 6 status is a maintenance failure, not
evidence that the platform cannot support the application.

### C. Reduced-scope Go reboot

- **Effort:** 90-150 engineer-weeks for a Docker-first MVP.
- **Benefit:** Removes legacy architecture and .NET while delivering a maintainable
  core.
- **Cost:** Deliberately drops long-tail integrations and native platform parity.
- **Best when:** No .NET is strategic and a funded team accepts reduced scope.

### D. Full parity rewrite

- **Effort:** 280-450+ engineer-weeks.
- **Benefit:** Maximum compatibility with a clean implementation.
- **Cost:** Highest delay, regression risk, and opportunity cost; still does not solve
  metadata by itself.
- **Best when:** There is durable funding, a large user base, contractual parity
  requirements, and several years of committed ownership.

## Is it worth it?

### Full parity rewrite: no

It is not worth funding today. The expected cost is too high relative to the evidence
of demand, the metadata dependency is not yet de-risked, and maintained forks provide
a much cheaper route to working software. The rewrite would spend most of its budget
recreating mature behavior rather than improving the product.

### Reduced-scope non-.NET reboot: conditionally yes

It can be worth it if all of these are true:

1. Removing .NET is a hard strategic constraint, not a preference.
2. The project can fund at least three experienced contributors for 12 months.
3. Metadata passes the Phase 0 corpus and reliability gates.
4. Docker/Linux, SQLite, a few download clients, and fewer notifications are an
   acceptable first release.
5. Existing databases are migrated through a one-way importer, not opened in place.
6. The team accepts that the old app remains the behavioral oracle during migration.
7. Governance and long-term maintenance ownership are established before coding.

If any of those conditions fail, use or contribute to an active fork instead.

## Recommended decision

Approve only **Phase 0** now.

At its end, choose one of two paths:

- If metadata and maintainer demand are strong, fund the reduced-scope Go reboot and
  reassess every 12 weeks against working vertical slices.
- If they are not, adopt a maintained fork and invest effort in metadata adapters,
  tests, documentation, and selective modernization rather than a rewrite.

Do not start by translating controllers or entities. Start with metadata viability,
contract capture, and a complete end-to-end slice: search an author, monitor a book,
find a release, send it to one download client, import it safely, and update the UI.
That slice will expose the real cost before the project commits to years of work.

## Repository references

- `README.md`: retirement reason and product scope.
- `src/Directory.Build.props`: target frameworks and ten runtime identifiers.
- `src/Directory.Packages.props`: backend dependency inventory.
- `src/NzbDrone.Host/Startup.cs`: HTTP, auth, middleware, SignalR, startup.
- `src/NzbDrone.Host/Bootstrap.cs`: hosting, services, TLS, utility modes.
- `src/NzbDrone.Core/Datastore`: custom repositories, dialects, mappings, migrations.
- `src/NzbDrone.Core/Datastore/TableMapping.cs`: 41 entity mappings and converters.
- `src/NzbDrone.Core/Messaging`: commands and domain events.
- `src/NzbDrone.Core/Jobs/TaskManager.cs`: recurring job definitions.
- `src/NzbDrone.Core/Download`: clients, queue, tracking, import completion.
- `src/NzbDrone.Core/MetadataSource`: metadata coupling and API contract.
- `src/NzbDrone.Core/MediaFiles`: scans, import, transfer, permissions.
- `src/NzbDrone.Core/Update`: built-in update workflow.
- `src/NzbDrone.Update`: out-of-process update client.
- `frontend/src/Components/SignalRConnector.js`: realtime UI coupling.
- `frontend/src/App/AppRoutes.js`: current UI surface.
- `build.sh` and `test.sh`: build, package, and test matrix.

## External sources

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [Node.js release schedule](https://github.com/nodejs/Release/blob/main/schedule.json)
- [Node.js single-executable applications](https://nodejs.org/api/single-executable-applications.html)
- [Go release policy and history](https://go.dev/doc/devel/release)
- [Rust platform support](https://doc.rust-lang.org/nightly/rustc/platform-support.html)
- [Open Library API documentation](https://openlibrary.org/developers/api)
- [Open Library monthly data dumps](https://openlibrary.org/developers/dumps)
- [Google Books API](https://developers.google.com/books/docs/v1/using)
- [rreading-glasses metadata compatibility service](https://github.com/blampe/rreading-glasses)
- [Bookshelf community revival](https://github.com/pennydreadful/bookshelf)
- [Faustvii Readarr fork](https://github.com/Faustvii/Readarr)
- [GNU GPL FAQ on language translations](https://www.gnu.org/licenses/gpl-faq.html#TranslateCode)
