---
name: "Save Session"
description: "Save the current session state to memory so it can be resumed later. Use when: finishing work, end of day, before closing VS Code, save session, checkpoint progress."
agent: agent
---

Summarize and save the current session to `.github/memory/session/session-{{currentDate}}.md` inside the project root.

Include:
1. **Status** — what is complete, in-progress, or blocked
2. **What was done** — key actions taken this session (bullet points)
3. **Current state** — what is running, what files were changed, relevant URLs/ports
4. **To resume** — exact commands to pick up where we left off
5. **Open questions / next steps** — anything unresolved

If a session file for today already exists, update it (use replace_string_in_file) rather than creating a new one.
Use `create_file` for new files, writing to the absolute path under the project root.

After saving, confirm with: "Session saved to `.github/memory/session/session-{{currentDate}}.md`."

Use today's date in YYYY-MM-DD format for `{{currentDate}}`.
