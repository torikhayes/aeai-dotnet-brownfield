---
name: "New Session"
description: "Archive the current session and start fresh. Use when: starting new work, new feature, new task, clean slate, archive old session."
agent: agent
---

Archive the current session and start fresh.

Steps:
1. List files in `.github/memory/session/` using `file_search` or `list_dir`
2. If any session files exist, read the most recent one and append a condensed summary to `.github/memory/repo/session-archive.md` (create if it doesn't exist) with today's date as a header
3. Delete all files in `.github/memory/session/` using the terminal: `rm .github/memory/session/*.md`
4. Confirm: "Previous session archived. Starting fresh — what are we working on today?"

Do not delete `.github/memory/repo/` files — those are permanent project notes.
