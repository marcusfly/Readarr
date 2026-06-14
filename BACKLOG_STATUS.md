# Readarr Backlog Status Register

This file is the canonical status source for AI agents. Keep it aligned with the
GitHub project board and update it whenever an item changes state or makes
material progress.

Status meanings:
- `Done`: implementation complete and validated.
- `In review`: implementation exists but still needs validation or follow-up.
- `Ready`: next item to pick up.
- `Backlog`: not started yet.

## Current Register

| # | Item | Board Status | Progress | Next Action | Evidence |
|---|---|---|---|---|---|
| 1 | BLI001 - Rebuild Metadata and Identity | Done | Complete | Keep as reference; no active work. | `BACKLOG_ITEM_1_VALIDATION_STATUS.md` |
| 2 | BLI002 - Upgrade the Backend to .NET 10 | Done | Complete | Keep as reference; production validated. | `AGENT_LOG.md` (2026-06-14T14:35:00) |
| 3 | BLI003 - Establish End-to-End Workflow Tests | Done | Complete | Keep as reference; item 3 plan is historical. | `AGENT_LOG.md`, `ITEM3_PLAN.md` |
| 4 | BLI004 - Harden Release Parsing and Matching | Done | Complete | Keep as reference; production validated. | `BACKLOG_ITEM_4_COMPLETE.md` |
| 5 | BLI005 - Make File Import Crash-Safe | Backlog | Not started | Design durable import state and recovery model. | `BACKLOG.md` |
| 6 | BLI006 - Standardize Download Client Integrations | Backlog | Not started | Define the download-client contract and capability model. | `BACKLOG.md` |
| 7 | BLI007 - Simplify Persistence and Migrations | Backlog | Not started | Document canonical schema and add migration tests. | `BACKLOG.md` |
| 8 | BLI008 - Replace Thread-Based Commands with Durable Jobs | Backlog | Not started | Define durable job state and persistence model. | `BACKLOG.md` |
| 9 | BLI009 - Modernize the Frontend Incrementally | Backlog | Not started | Break frontend modernization into isolated steps. | `BACKLOG.md` |
| 10 | BLI010 - Modernize Packaging and Releases | Backlog | Not started | Define release matrix, signing, and verification flow. | `BACKLOG.md` |

## Notes

- The GitHub project board mirrors this register.
- When an agent starts work on an item, update this file, `BACKLOG.md`, the
  project board, and `AGENT_LOG.md` first, then continue work.
- When an agent makes progress, add a short progress note here and in
  `AGENT_LOG.md`, and update the project draft body metadata if the scope or
  next action changed.
- Historical validation docs remain as evidence, but this register is the source
  of truth for current status.
- Each GitHub project draft item should include a metadata block in its body with
  the item ID, board status, progress, dependencies, evidence, and next action.
- GitHub project fields named `Created` and `Updated` are system-managed in this
  project; if you need explicit timestamps, record them in the draft body metadata.
- New backlog items must be assigned the next `BLI###` number in creation order.
