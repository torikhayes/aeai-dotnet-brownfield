---
name: "Sync Skills"
description: "Audit and sync skill/prompt parity between Copilot and Claude. Creates bridging files so neither toolset is left behind. Use when: skills are out of sync, added a new Claude skill, added a new Copilot agent, checking parity."
agent: agent
---

Read the agent definition at `.github/agents/sync-skills.agent.md` for full instructions, then execute the workflow from Step 1.

Arguments passed to this command (if any) are forwarded to the agent:
- `report` — report gaps only, do not create files
- `copilot-only` — only create missing Copilot prompts (Step 4a)
- `claude-only` — only create missing Claude skills (Step 4b)
