# Agent Collaboration Rules

These rules apply to every human or AI contributor working in this repository.

## Principal Rule: Keep a Project Diary

Maintain `AGENT_LOG.md` as the shared, append-only record of active work, decisions,
verification, blockers, and handoffs. The purpose is continuity without requiring
another contributor to reconstruct prior work.

## Principal Rule: Keep the Backlog Current

All task tracking is done only in GitHub projects.

When backlog work changes:

- Use the GitHub project item and project item title/body as the source of truth.
- Keep titles on the project board in `BLI### - ...` format.
- Add a concise progress note to `AGENT_LOG.md` describing the change.
- If the board cannot be updated, record a `BLOCKED` entry in `AGENT_LOG.md` immediately.

Before starting work:

1. Read the latest entries in `AGENT_LOG.md`.
2. Run `git status --short --branch` and inspect existing changes.
3. Add a short `START` entry naming the task and files or areas likely to change.
4. Do not overwrite, revert, or reformat another contributor's uncommitted work.

While working:

- Add a `DECISION` entry when making a non-obvious architectural or behavioral choice.
- Add a `BLOCKED` entry when progress depends on user input, credentials, external
  state, or missing tooling.
- Keep entries factual and concise. Do not log hidden reasoning, secrets, tokens,
  credentials, personal data, or large command output.
- Prefer one active writer per file. If another agent claims the same area, stop and
  coordinate through the log or user before editing it.

### Required GitHub task workflow for all agents

Before starting backlog work, when making progress, and before stopping:

1. Set token for `gh`:
   - `setx`/persist your token outside the repo.
   - ` $env:GH_TOKEN = "<token>"` (or your standard CI/CLI auth mechanism).
2. Confirm identity:
   - `gh auth status`
3. Locate project and item:
   - `gh project list --owner @me`
   - `gh project item-list <project-id-or-number> --owner @me --format json`
4. Update the item state directly in GitHub:
   - `gh project item-edit <item-id> --project <project-id-or-number> --owner @me --title "BLI### - ..."`
   - `gh project item-edit <item-id> --project <project-id-or-number> --owner @me --body "<current task metadata>"`

If GitHub is unavailable, record a `BLOCKED` entry in `AGENT_LOG.md` with the
command error immediately.

Before yielding:

1. Add a `HANDOFF` or `DONE` entry.
2. List files changed, tests or checks run, failures, and the exact next action.
3. Record whether changes are uncommitted, committed, or pushed.
4. Leave the worktree in a state another contributor can inspect safely.

Use timestamps in ISO 8601 with the local UTC offset. Never rewrite or delete prior
entries; append a correction if an earlier entry is wrong.

## Repository Snapshot

- Working branch: `develop-mfly`, tracking `origin/develop-mfly`.
- Backend: C#/.NET solution at `src/Readarr.sln`, targeting .NET 6.
- Frontend: React 17 and TypeScript under `frontend`, built with Webpack 5.
- Pinned frontend tools: Node 20.11.1 and Yarn 1.22.19 (see `package.json`).
- Main frontend checks: `yarn lint`, `yarn stylelint-windows`, and `yarn build`.
- Main backend build entry point: `build.sh`; test runner: `test.sh`.

## Scope and Verification

- Keep changes narrowly scoped and preserve established project patterns.
- Never discard unrelated work in a dirty worktree.
- Run the smallest relevant checks first, then broaden verification when shared
  behavior or cross-module contracts change.
- If required tooling is unavailable, document that fact in `AGENT_LOG.md` rather
  than claiming verification.
