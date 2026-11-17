# Agent entry point

Read `.github/copilot-instructions.md` first, then the scoped file matching the paths you edit:

- `api/Features|Controllers|Infrastructure|Common|Shared/**` → `.github/instructions/api.instructions.md`

- `api/Importing/**` → `.github/instructions/importers.instructions.md` plus any matching `.github/instructions/{swu,swccg,pokemon,mtg}-importer.instructions.md`; use each file's `applyTo` patterns to determine whether it matches
- `api/Features/Admin/Sync/**`, `api/Infrastructure/Auth/Admin*.cs` → `.github/instructions/admin.instructions.md`
- `api/Data|Migrations|Models/**` → `.github/instructions/database.instructions.md`
- `client-vite/src/**` → `.github/instructions/client.instructions.md`
- `api.Tests/**`, `client-vite/src/**/__tests__/**` → `.github/instructions/testing.instructions.md`

If an edited path matches no pattern above, follow only `.github/copilot-instructions.md` among the instruction files.

If a path matches more than one scoped file, read all of them; on conflict, the more specific pattern wins (admin > api; testing > client; game-specific importer > importers).

## Context & Token Guardrails
- **Code Reviews**: Never scan the full workspace or read unmodified files unless verifying a direct caller/callee contract changed in the diff.
- **Excluded Paths**: Never read or review paths matched by `.copilotignore` (e.g., `**/bin/`, `**/obj/`, `**/node_modules/`, `**/dist/`, `**/TestResults/`, `*.trx`, `Imports/`, `api/Migrations/*.Designer.cs`, `api/Migrations/AppDbContextModelSnapshot.cs`, lockfiles). The file is advisory (not read natively by any tool); this rule is what enforces it.
- **Targeted Verification**: During iteration, run only the specific test class (`dotnet test --filter`) or test file (`npx vitest run <path>`) covering the edited code; reserve full suite runs for final pre-PR verification.
