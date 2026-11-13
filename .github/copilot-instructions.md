# Trading Card Game Tracker

Local single-user app. Stack: .NET 10 / ASP.NET Core / EF Core 10 (SQLite) in `api/`; React + Vite + TypeScript + TanStack Query + Tailwind/shadcn in `client-vite/`; npm (CI uses npm; `package-lock.json`); xUnit (`api.Tests/`) + Vitest.

Precedence: scoped file > this file > CONTRIBUTING/README. If CI (`.github/workflows/ci.yml`) differs from these commands, follow CI and report it.
Path-specific rules: `.github/instructions/{api,client,database,importers,swu-importer,swccg-importer,pokemon-importer,admin,testing}.instructions.md`.

## Scope
- Shared models/services stay game-aware (use `GameId`); never hardcode game specific assumptions into shared code.

## Domain invariants
- Collections, decks and wishlists reference shared `CardPrinting` IDs, never `SwuCardPrinting`. Preserve these IDs.
- API keeps the `Style` field (maps to `Treatment.Name`); persistence uses nullable `TreatmentId`.
- Sets are unique on `(GameId, Code)`.
- `UserCard.IsProxy = true` is excluded from all value calculations.

## Security
- Trust forwarded headers only from configured `KnownProxies`/`KnownNetworks` within `ForwardLimit`; client IP = rightmost untrusted `X-Forwarded-For`.
- Never authorize on `Host`. Never hardcode secrets (env or git-ignored config only).

## Cross-layer rules
- Changing a response DTO requires updating its TS type in `client-vite/src/` in the same change.
- A new endpoint requires a function in `client-vite/src/features/api/` and a key in `lib/queryKeys.ts`.
- Model changes require an EF migration in the same change. Never edit or delete applied migrations; schema changes are additive.
- Do not break routes/response shapes the client uses; if unavoidable, change API and client together.

## Errors
- API: unhandled exceptions become RFC 7807 `ProblemDetails` via `DefaultProblemDetailsFactory`; never expose exception text. Log business-rule failures at `Warning`, unexpected ones at `Error`.
- Client: route components sit under `ErrorBoundary`; show friendly messages and `toast` for mutations; never render raw error objects.

## Code review & context scope
- Scope code reviews to the requested diff: staged changes (`git diff --staged`) by default, or the commit range / PR the user names. Inspect surrounding files only when signature or contract changes require it.
- Never read, index, or review paths in `.copilotignore` (`Imports/`, `**/TestResults/`, `*.trx`, `api/Migrations/*.Designer.cs`, `AppDbContextModelSnapshot.cs`, `**/node_modules/`, `**/dist/`, `**/bin/`, `**/obj/`, lockfiles). The file is advisory: no tool reads it natively, so these instructions are what enforce it.

## Commands (CI runs all listed checks; locally, run the touched half)
```bash
dotnet build ./api/api.sln -c Release -warnaserror
dotnet format ./api/api.sln --verify-no-changes
ASPNETCORE_ENVIRONMENT=Testing dotnet test ./api/api.sln -c Release
cd client-vite && npm run lint:strict && npm run typecheck && npm run format && npm test -- --run && npm run build
```
During iteration, run targeted tests (`dotnet test --no-build --filter "FullyQualifiedName~<Class>"` or `cd client-vite && npx vitest run <file>`). Before a PR, run the full check suite for the touched stack (or both when a DTO or route changes).
Run API: `dotnet run --project ./api/api.csproj --launch-profile "TradingCardApi (HTTPS)"`.
Run client: `cd client-vite && npm run dev`; needs `client-vite/.env.local` with `VITE_API_BASE=https://localhost:7226/api`.

## Branches and PRs
- Branch from and target `master` (there is no `dev` branch).
- One concern per PR; use `.github/PULL_REQUEST_TEMPLATE.md`; list the exact commands run and their results; flag any migration.

## Stop and ask the user before
- Adding, removing or editing a migration, or any destructive data change.
- Changing auth, admin-session, CSRF or forwarded-header handling.
- Changing an existing route or response shape.
- Adding a dependency.
- Any failing CI-equivalent command you cannot fix in two attempts.

## Final report must contain
Files changed, commands run with pass/fail, commands not run and why, and any open question.

## Docs
Public service methods get `/// <summary>`; exported hooks, API and utility functions get JSDoc. Explain *why*, not *what*.
