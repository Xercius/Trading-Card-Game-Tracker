# Trading Card Game Tracker

Local single-user app. Stack: .NET 10 / ASP.NET Core / EF Core 10 (SQLite) in `api/`; React + Vite + TypeScript + TanStack Query + Tailwind/shadcn in `client-vite/`; pnpm; xUnit (`api.Tests/`) + Vitest.

Path-specific rules: `.github/instructions/{api,client,database,testing}.instructions.md`.

## Scope
- Work on **Star Wars Unlimited (SWU) only**. Do not edit non-SWU importers, tests, docs or references unless one blocks SWU from running; then make the smallest possible change.
- Shared models/services stay game-aware (use `GameId`); never hardcode SWU assumptions into shared code.

## Domain invariants
- Collections, decks and wishlists reference shared `CardPrinting` IDs, never `SwuCardPrinting`. Preserve these IDs.
- SWU printing identity is `CardUid`; set/number/style fallback only adopts legacy printings without a UID.
- API keeps the `Style` field (maps to `Treatment.Name`); persistence uses nullable `TreatmentId`.
- SWU aspect multiplicity lives in `CardAspect.Count`, never duplicate rows.
- Sets are unique on `(GameId, Code)`.
- `UserCard.IsProxy = true` is excluded from all value calculations.
- Importers/syncs must be idempotent upserts and support `dryRun` (dry runs write a sync log but no checkpoint).

## Security
- Trust forwarded headers only from configured `KnownProxies`/`KnownNetworks` within `ForwardLimit`; client IP = rightmost untrusted `X-Forwarded-For`.
- Never authorize on `Host`. Never hardcode secrets (env or git-ignored config only).
- Admin sync endpoints require an AdminSession cookie + CSRF, or AdminToken. The client user selector is not an admin identity.

## Cross-layer rules
- Changing a response DTO requires updating its TS type in `client-vite/src/` in the same change.
- A new endpoint requires a function in `client-vite/src/features/api/` and a key in `lib/queryKeys.ts`.
- Model changes require an EF migration in the same change. Never edit or delete applied migrations; schema changes are additive.
- Do not break routes/response shapes the client uses; if unavoidable, change API and client together.

## Errors
- API: unhandled exceptions become RFC 7807 `ProblemDetails` via `DefaultProblemDetailsFactory`; never expose exception text. Log business-rule failures at `Warning`, unexpected ones at `Error`.
- Client: route components sit under `ErrorBoundary`; show friendly messages and `toast` for mutations; never render raw error objects.

## Commands (all must pass before a PR)
```bash
dotnet format ./api/api.sln --verify-no-changes
ASPNETCORE_ENVIRONMENT=Testing dotnet test ./api/api.sln -c Release
pnpm --filter client-vite lint && pnpm --filter client-vite typecheck && pnpm --filter client-vite format
pnpm --filter client-vite exec vitest run
```
Run API: `dotnet run --project ./api/api.csproj --launch-profile "TradingCardApi (HTTPS)"`.
Run client: `pnpm --filter client-vite dev`; needs `client-vite/.env.local` with `VITE_API_BASE=https://localhost:7226/api`.

## PRs
One concern per PR; follow `.github/PULL_REQUEST_TEMPLATE.md`; list test commands run; note any migration.

## Docs
Public service methods get `/// <summary>`; exported hooks, API and utility functions get JSDoc. Explain *why*, not *what*.
