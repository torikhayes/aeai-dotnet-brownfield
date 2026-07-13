---
name: "Continue Session"
description: "Resume work from the last saved session. Use when: continuing from last session, picking up where I left off, resume, restart VS Code, what were we doing."
agent: agent
---

Load and summarize the last saved session so we can pick up where we left off.

Steps:
1. List all files in `.github/memory/session/` (use `file_search` or `list_dir` on the project path) and identify the most recent one
2. Read that file using `read_file`
3. Also read files in `.github/memory/repo/` for project-specific context
4. Present a concise summary:
   - What was being worked on
   - Current state (what's running, what's pending)
   - Exact commands to resume (e.g. start the app)
5. Ask: "Ready to continue — shall I run the resume commands?"

If no session files exist, say so and offer to start fresh.
