<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read specs/001-catalog-domain-rebrand/plan.md
<!-- SPECKIT END -->

## Codebase Context Files

Before exploring the codebase directly for **any task**, consult the pre-generated context files in `.github/context/`. These files document the architecture, API endpoints, database schemas, integration events, and key class structure for every service in the monorepo.

**Default lookup order:**
1. Read `.github/context/eshop-overview.md` — system architecture map, service directory, event flow, and agent recommendations
2. Read the service-specific context file(s) relevant to the task (e.g., `.github/context/catalog-api.md`, `.github/context/ordering-api.md`)
3. Only fall back to reading source files directly when the context files lack the specific detail needed

**Exception:** If the user explicitly attaches or references a specific source file in the chat, use that file directly — context file lookup is not required.

Available context files: `README.md`, `eshop-overview.md`, `catalog-api.md`, `basket-api.md`, `ordering-api.md`, `order-processor.md`, `payment-processor.md`, `identity-api.md`, `webapp.md`, `webhooks-api.md`, `webhook-client.md`, `apphost.md`, `shared-libraries.md`

## Skills

- **local-setup** (`.claude/skills/local-setup/SKILL.md`) — Use for any local setup, onboarding, or "how do I run this?" questions. Reads `.github/prompts/local-setup.prompt.md` as the single source of truth (shared with GitHub Copilot).
- **db-lifecycle** (`.claude/skills/db-lifecycle/SKILL.md`) — Use for checking migration status, running migrations, seeding data, or starting the app. Reads `.github/prompts/db-lifecycle.prompt.md` as the single source of truth (shared with GitHub Copilot).
- **run-unit-tests** (`.claude/skills/run-unit-tests/SKILL.md`) — Use for running unit tests. Reads `.github/agents/run-unit-tests.agent.md` as the single source of truth (shared with GitHub Copilot).
- **speckit-testplan** (`.claude/skills/speckit-testplan/SKILL.md`) — Generate a markdown test plan (TC-### test cases) from a feature's checklist items. Run after `/speckit-checklist`.
- **speckit-scripttest** (`.claude/skills/speckit-scripttest/SKILL.md`) — Generate shell automation scripts (tc-{NNN}.sh + run-all.sh) from a feature's test plan markdown files. Spawns sub-agents per markdown file. Run after `/speckit-testplan`.
- **speckit-runtests** (`.claude/skills/speckit-runtests/SKILL.md`) — Run the shell-scripted tests for the currently active feature spec. Executes tc-*.sh via run-all.sh and reports pass/fail. Run after `/speckit-scripttest`.
- **run-full-suite** (`.claude/skills/run-full-suite/SKILL.md`) — Discover and run all speckit shell test suites across every feature spec in the repo. Produces a consolidated cross-spec report.
