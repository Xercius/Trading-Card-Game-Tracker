# Agent entry point

Read `.github/copilot-instructions.md` first, then the scoped file matching the paths you edit:
- `api/Features|Controllers|Infrastructure|Common|Shared/**` → `.github/instructions/api.instructions.md`
- `api/Importing/**`, `api/Features/Admin/**` → `.github/instructions/importers.instructions.md`
- `api/Data|Migrations|Models/**` → `.github/instructions/database.instructions.md`
- `client-vite/src/**` → `.github/instructions/client.instructions.md`
- `api.Tests/**`, `client-vite/src/**/__tests__/**` → `.github/instructions/testing.instructions.md`

Precedence: scoped file > root file > CONTRIBUTING.md/README. CI (`.github/workflows/ci.yml`) is the source of truth for gate commands; if they differ, follow CI and report the difference.
