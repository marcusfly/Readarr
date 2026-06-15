# Readarr Modernization Backlog

Backlog tasks are not tracked in this repository.

## Source of truth

- GitHub Project: `https://github.com/users/<YOUR_GH_USERNAME>/projects/1`
- Prefer this repo-local pointer:
  - `BACKLOG_STATUS.md` (contains GH access commands only)

## How to access current tasks

1. Authenticate with GitHub CLI:
   - `gh auth status`
2. Confirm the target project:
   - `gh project list --owner @me`
   - `gh project item-list <project-id-or-number> --owner @me --format json`
3. Open and inspect task bodies in the project UI or filter with JSON:
   - `gh project item-list <project-id-or-number> --owner @me --format json`

## Agent update rule

All agents must make task updates directly in GitHub project items and then log the
work in `AGENT_LOG.md`.

Do not add task status, next actions, or progress bullets in local files.
