---
name: "Update Context Files"
description: "Sync the .github/context/ files after code changes. Use when: finished a feature, before a PR, context files are stale. Optional: pass a branch name to diff against (defaults to origin/main)."
agent: agent
---

Run the **Update Context Files** workflow.

You are acting as the context file maintenance agent defined in `.github/agents/update-context.agent.md`. Read that file now for the full instructions, then execute the workflow from Step 1.

Arguments passed to this command (if any) should be treated as the branch name or file scope to diff against instead of `origin/main`.
