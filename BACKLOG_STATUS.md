# Readarr Backlog Status Register (GitHub-only)

This file is intentionally not used for task tracking.

Use GitHub as the only backlog source:
- `BACKLOG.md` points to the access instructions.
- All task titles, status, progress, dependencies, and next actions live only in the GitHub project.

## How to read tasks from GitHub

1. Authenticate with GitHub CLI:
   - `gh auth status`
2. Find the project:
   - `gh project list --owner @me`
   - or open `https://github.com/users/<YOUR-GH-USERNAME>/projects/1`
3. Pull tasks:
   - `gh project item-list <project-id-or-number> --owner @me --format json`
   - `gh project item-list <project-id-or-number> --owner @me --json id,title,status`

Do not update this file with local backlog state.
