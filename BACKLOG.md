# Readarr Modernization Backlog

This backlog is ordered by product viability, risk reduction, and dependency order.
Items should be reassessed after each completed priority. Removing .NET is not itself
a priority until the product-critical risks and behavioral contracts are controlled.

## 1. Rebuild Metadata and Identity

**Status:** Complete for the fresh-install contract as of 2026-06-13.

**Goal:** Make author, work, edition, series, ISBN, ASIN, and provider identities
stable and independent of Goodreads.

**Work:**

- Define provider-neutral, namespaced identifiers.
- Create deterministic matching, merge, redirect, and deletion rules.
- Add a versioned metadata provider interface.
- Support rreading-glasses first and evaluate at least one independent provider.
- Add local caching, outage handling, provenance, and refresh cursors.
- Build a representative metadata acceptance corpus.
- Treat the redesign as fresh-install-only; reconcile editions across providers by
  normalized ISBN rather than migrating Goodreads-derived ownership.

**Done when:** Search and refresh pass agreed coverage and correctness thresholds,
new records retain stable namespaced identity, and loss of one provider does not
corrupt a library.

## 2. Upgrade the Backend to .NET 10

**Goal:** Align the backend with Sonarr's forward baseline and keep the stack on a supported release train.

**Work:**

- Upgrade from the old .NET 6-era baseline to .NET 10.
- Update incompatible dependencies and build targets.
- Preserve Windows, Linux, macOS, SQLite, and PostgreSQL behavior.
- Document dependency replacements and unavoidable compatibility changes.

**Done when:** Supported targets build and start successfully, relevant backend tests
pass, and release artifacts no longer require an unsupported runtime.

## 3. Establish End-to-End Workflow Tests

**Goal:** Capture the behavior that must survive modernization or replacement.

**Plan:** See [ITEM3_PLAN.md](ITEM3_PLAN.md) for the current handoff plan and execution order.

**Work:**

- Cover search, monitor, release selection, download, completion, import, rename, and
  UI update as one workflow.
- Build reusable fixtures for API payloads, parser inputs, webhooks, and databases.
- Add test containers or simulators for priority indexers and download clients.
- Test interrupted jobs, retries, restarts, duplicates, and partial file operations.

**Done when:** The critical workflow runs repeatably in CI and fails on meaningful
behavioral regressions.

## 4. Harden Release Parsing and Matching

**Goal:** Select and associate releases with the correct author, work, and edition.

**Work:**

- Convert existing parser cases into a language-neutral corpus.
- Separate parsing, normalization, candidate generation, and matching.
- Add explicit confidence and rejection reasons.
- Cover multiple languages, audiobooks, ebooks, collections, editions, and malformed
  releases.
- Measure false-positive and false-negative rates.

**Done when:** The parser meets agreed accuracy thresholds on the corpus and every
selection or rejection is explainable.

## 5. Make File Import Crash-Safe

**Goal:** Prevent lost, duplicated, partially moved, or incorrectly renamed files.

**Work:**

- Model import as an idempotent operation with durable state.
- Add a file-operation journal and recovery procedure.
- Validate source, destination, free space, permissions, and expected file identity.
- Handle copy, move, hardlink, cross-device, network-share, and locked-file behavior.
- Add dry-run output and reversible rename plans.

**Done when:** Forced interruption at every import stage can be resumed or rolled back
without losing the source file or producing an untracked destination.

## 6. Standardize Download Client Integrations

**Goal:** Reduce adapter duplication and make queue behavior predictable.

**Work:**

- Define a versioned download-client contract and capability model.
- Standardize authentication, retries, timeouts, categories, tags, and error mapping.
- Normalize queue states, completion, seeding, removal, and failure behavior.
- Make remote-path mapping and reconciliation explicit.
- Prioritize adapters using measured user demand.

**Done when:** Priority clients pass the same contract suite and recover correctly
after client or application restarts.

## 7. Simplify Persistence and Migrations

**Goal:** Make schema evolution, backup, restoration, and database behavior safer.

**Work:**

- Document the canonical schema and invariants.
- Reduce custom mapping, implicit lazy loading, and dialect-specific surprises.
- Add migration tests using real historical backup samples.
- Validate SQLite first and retain PostgreSQL through explicit contract tests.
- Add integrity reports, dry-run migration, backup verification, and recovery tests.

**Done when:** Representative historical databases upgrade reproducibly with verified
counts and relationships, and failed upgrades leave a usable rollback path.

## 8. Replace Thread-Based Commands with Durable Jobs

**Goal:** Make background work observable, restart-safe, cancelable, and idempotent.

**Work:**

- Define explicit queued, running, retrying, completed, failed, and canceled states.
- Persist attempts, leases, progress, idempotency keys, and terminal results.
- Add concurrency controls for authors, books, downloads, and file paths.
- Support graceful shutdown, restart recovery, backoff, and dead-letter handling.
- Expose job state consistently through the API and realtime events.

**Done when:** Restarting or crashing the process cannot silently lose work or execute
the same destructive operation twice.

## 9. Modernize the Frontend Incrementally

**Goal:** Move to a supported, typed frontend without combining it with a full rewrite.

**Work:**

- Abstract the SignalR-specific realtime connector.
- Generate client types from the API contract.
- Upgrade React, routing, state, build, lint, and test tooling in isolated steps.
- Move from Webpack to a current supported toolchain.
- Convert JavaScript to strict TypeScript by feature area.
- Remove jQuery and Moment when their owning features are modernized.

**Done when:** The client uses supported dependencies, new feature code is typed, and
the main UI workflows pass automated browser tests.

## 10. Modernize Packaging and Releases

**Goal:** Produce reproducible, secure, supportable releases with a manageable platform
matrix.

**Work:**

- Start with Linux amd64/arm64 containers and measured platform priorities.
- Produce signed artifacts, checksums, SBOMs, and provenance.
- Automate vulnerability scanning and release verification.
- Define configuration, data-volume, backup, and external-update behavior.
- Add native Windows, macOS, and Linux packaging only where demand justifies it.
- Document upgrade, rollback, and support windows.

**Done when:** A clean environment can build, verify, install, upgrade, and roll back a
release using documented automation.

## Recommended First Increment

Execute priorities 1-3 together:

1. Prove metadata viability and stable identity.
2. Move the existing backend onto a supported runtime.
3. Capture the critical end-to-end behavior before deeper architectural changes.

This increment determines whether continued modernization is justified and provides
the safety net required for every later backlog item.
