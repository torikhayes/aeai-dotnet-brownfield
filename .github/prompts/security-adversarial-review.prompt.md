---
name: "Security Adversarial Review"
description: "Actively try to break a running, isolated instance of the application with malformed input, boundary conditions, and deliberate misuse, and report any runtime issue found with exact reproduction steps. Refuses to run against anything but a loopback target. Trigger phrases: adversarial review, penetration test, try to break the app, fuzz the app, red team the running app."
agent: agent
---

Read the Claude skill definition at `.claude/skills/security-adversarial-review/SKILL.md` for full instructions, then execute that workflow.

Arguments passed to this command (if any) should be forwarded as-is to the skill.
