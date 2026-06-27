# Codex Project Instructions

Read and follow `AGENTS.md` before making changes.

The project diary in `AGENT_LOG.md` is mandatory for coordination with Claude
and human contributors. Add a `START` entry before editing and a `HANDOFF` or
`DONE` entry before yielding. Preserve all existing uncommitted work.

Backlog state is maintained only in GitHub project items. Use `BACKLOG.md` for
GH task-access instructions and read task details directly from GitHub.

Speed up delivery by defaulting to parallel execution. When a task can be split
safely, launch multiple focused sub-agents early to explore, verify, or prepare
independent slices of work in parallel, then consolidate the results yourself.
Only stay single-agent when the work is truly linear or shared-file contention
would create more risk than speed.
