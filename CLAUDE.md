<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read specs/001-catalog-domain-rebrand/plan.md
<!-- SPECKIT END -->

## Skills

- **local-setup** (`.claude/skills/local-setup/SKILL.md`) — Use for any local setup, onboarding, or "how do I run this?" questions. Reads `.github/agents/local-setup.agent.md` as the single source of truth (shared with GitHub Copilot).
- **db-lifecycle** (`.claude/skills/db-lifecycle/SKILL.md`) — Use for checking migration status, running migrations, seeding data, or starting the app. Reads `.github/agents/db-lifecycle.agent.md` as the single source of truth (shared with GitHub Copilot).
- **run-unit-tests** (`.claude/skills/run-unit-tests/SKILL.md`) — Use for running unit tests. Reads `.github/agents/run-unit-tests.agent.md` as the single source of truth (shared with GitHub Copilot).
- **speckit-testplan** (`.claude/skills/speckit-testplan/SKILL.md`) — Generate a markdown test plan (TC-### test cases) from a feature's checklist items. Run after `/speckit-checklist`.
- **speckit-scripttest** (`.claude/skills/speckit-scripttest/SKILL.md`) — Generate shell automation scripts (tc-{NNN}.sh + run-all.sh) from a feature's test plan markdown files. Spawns sub-agents per markdown file. Run after `/speckit-testplan`.
- **speckit-runtests** (`.claude/skills/speckit-runtests/SKILL.md`) — Run the shell-scripted tests for the currently active feature spec. Executes tc-*.sh via run-all.sh and reports pass/fail. Run after `/speckit-scripttest`.
- **run-full-suite** (`.claude/skills/run-full-suite/SKILL.md`) — Discover and run all speckit shell test suites across every feature spec in the repo. Produces a consolidated cross-spec report.
