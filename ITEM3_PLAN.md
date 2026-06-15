# Item 3 Plan

**Status:** Complete. This document is historical handoff context; the live status
register is [BACKLOG_STATUS.md](BACKLOG_STATUS.md).

## Objective
Capture the critical Readarr workflow in repeatable CI tests so modernization work cannot break user-visible behavior without detection.

## Scope
- Search
- Monitor
- Release selection
- Download completion
- Import
- Rename
- UI update

## Execution Order
1. Inventory the existing integration and fixture coverage around the full workflow.
2. Identify the minimum reusable seams for API payloads, parser inputs, webhooks, and database state.
3. Build one golden-path workflow test that drives search through import and rename.
4. Add recovery cases for retries, restarts, duplicates, interrupted jobs, and partial file operations.
5. Add test containers or simulators for the highest-value external dependencies.
6. Wire the workflow test into CI and keep it deterministic.

## Acceptance
- The critical workflow runs end to end in CI.
- Failures point to meaningful behavioral regressions, not harness noise.
- Fixture setup is reusable for later modernization items.

## Handoff Notes
- Current dirty work from item 1 and item 2 remains preserved.
- Item 3 implementation has started with typed integration clients for book-file
  and rename-preview queries, plus a refactor of `CriticalWorkflowFixture` to
  use fresh lookup results and local import/rename verification.
