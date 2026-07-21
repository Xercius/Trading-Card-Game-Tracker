# Copilot Instructions

## Project Overview
A local web application for tracking trading card collections, deck building, wishlists, and collection/deck values. Supports **Magic the Gathering**, **Star Wars Unlimited**, **Disney Lorcana**, **Flesh and Blood**, **Star Wars CCG**, **Guardians**, **Dicemasters**, **Pokémon TCG**, and **Transformers**. Cards are imported from external APIs/scrapers via 13 game-specific importers in `api/Importing/`.

> **Path-specific guidance:** Detailed coding standards live in focused instruction files that Copilot loads automatically when you work in the relevant area:
> - `api/**/*.cs` → `.github/instructions/api.instructions.md`
> - `client-vite/src/**/*.{ts,tsx}` → `.github/instructions/client.instructions.md`
> - `api/Migrations/**`, `api/Data/**`, `api/Models/**` → `.github/instructions/database.instructions.md`
> - `api.Tests/**`, `**/__tests__/**`, `**/*.test.{ts,tsx}` → `.github/instructions/testing.instructions.md`

## Setup
- Requires **.NET 10 SDK** (per `global.json`) and **Node.js 20+**.
- API
  ```bash
  dotnet restore ./api/api.csproj
  dotnet build ./api/api.csproj
  # Seed sample data (optional)
  cd api && dotnet run seed
  # Start API (HTTPS :7226, HTTP :5229)
  dotnet run --project ./api/api.csproj --launch-profile "TradingCardApi (HTTPS)"
  ```
- Client — create `client-vite/.env.local` first:
  ```
  VITE_API_BASE=https://localhost:7226/api
  ```
  ```bash
  pnpm install --filter client-vite...
  pnpm --filter client-vite dev   # http://localhost:5173
  ```

## Security
- Trust forwarded headers only from configured `KnownProxies`/`KnownNetworks` within `ForwardLimit`.
- Resolve client IP from rightmost `X-Forwarded-For` that is not a trusted proxy.
- Never authorize based on `Host` header.
- Secrets only via env or configuration files ignored by Git. Never hardcode credentials.

## Pull Requests
- One concern per PR. Keep diffs small.
- Describe commands used for testing and expected results.
- Run all tests locally before push.
- No route or schema changes without a migration and a note in the PR description.
- No breaking API changes without a versioning plan (see Backwards Compatibility below).

## Style / Formatting
- Follow `.editorconfig` and Prettier. 4-space indent (C#), 2-space (TS/JSON).
- Run `dotnet format` and `pnpm format` before commit.
- Keep imports ordered (framework → third-party → app modules → relative). Remove unused imports.
- CI gates: `dotnet format --verify-no-changes`, `pnpm lint`, `pnpm typecheck`, and all tests must pass.

## Error Handling Patterns

### API (C#)
- All unhandled exceptions are caught by global middleware and returned as RFC 7807 `ProblemDetails`.
- Use a central `ProblemDetailsFactory` for known exception types (400/404/409/403).
- Never return raw exception messages or stack traces to the client.
- Log unexpected exceptions at `Error` level with structured context (request path, user ID).
- Business-rule violations (not found, conflict) log at `Warning` level.

### Client (TypeScript)
- Wrap route-level components with `ErrorBoundary` to catch render-time errors.
- Display user-friendly messages for API errors — never surface raw error objects in the UI.
- Use `toast` notifications for mutation success/failure feedback.
- Log client-side errors to the browser console in development; integrate an error reporter in production if needed.

## Documentation Standards

### C# (XML Comments)
- Add `/// <summary>` XML comments to all `public` methods and properties in service classes and interfaces.
- Document non-obvious parameters with `/// <param name="...">`. Skip trivial getters/setters.
- Keep comments concise — explain *why*, not *what* (the code shows what).

### TypeScript (JSDoc)
- Add JSDoc comments (`/** */`) to all exported custom hooks, API functions, and utility functions.
- Document non-obvious props in component prop type definitions.
- Prefer self-documenting names over verbose comments.

## Backwards Compatibility & API Versioning
- This is a local single-user application. No public API versioning is required.
- Avoid breaking changes to existing API response shapes or route paths when the client depends on them.
- If a breaking change is necessary, update the client and API together in the same PR.
- Database schema changes must be non-destructive (additive). Use migrations with explicit data steps for column changes.
- Never delete or modify applied migration files.

## Deployment & Environment Configuration

### Environments
| Environment | Purpose |
|-------------|---------|
| `Development` | Local dev. Uses `appsettings.Development.json`. Hot-reload via `dotnet watch`. |
| `Testing` | Automated tests. Uses per-test SQLite DB. No external services called. |
| `Production` | Deployed instance. Uses env vars or production config files (not checked in). |

### Configuration Management
- Use `IOptions<T>` + `ValidateOnStart()` for all typed configuration sections.
- Sensitive config (JWT secret, external API keys) must be in environment variables or untracked config files.
- `appsettings.Development.json` may contain dev-only non-sensitive overrides and is `.gitignore`d for secrets.
- Client environment is configured via `client-vite/.env.local` (not committed). Only `VITE_` prefixed variables are exposed to the browser.

## Monorepo Coordination (API ↔ Client)
- When changing an API response DTO, update the corresponding TypeScript type in `client-vite/src/` in the same PR.
- When adding a new API endpoint, add the matching API client function in `client-vite/src/features/api/` and register the query key in `lib/queryKeys.ts`.
- Database migrations and the EF model change must be in the same PR as the API feature that uses them.
- Run both backend and frontend tests before pushing any PR that touches both layers.

---

## Feature Inventory

### API (`api/Features/`)

| Feature | Responsibility |
|---------|---------------|
| `Auth` | JWT login/logout, token issuance. |
| `Cards` | Card search (paged, filtered), facets, printing list, sparkline. |
| `Collections` | CRUD for `UserCard`; quick-add; collection value history. |
| `Decks` | Deck CRUD; add/remove cards; deck value history. |
| `Wishlists` | CRUD for `WishlistEntry`; quick-add. |
| `Prices` | Ingest price points; query price history. |
| `Values` | Aggregate daily value snapshots (collection + deck). |
| `Admin` | User management (CRUD, promote, demote, delete); card import tools; price ingest. |
| `Users` | User directory (read-only). |
| `Health` | `GET /api/health` liveness probe. |

**Admin guards:** `[RequireAdmin]` returns `403 ProblemDetails`. Prevents demoting/deleting the last admin (`409 Conflict`).

### Client (`client-vite/src/`)

| Module | Responsibility |
|--------|---------------|
| `features/cards` | Card search page, filters, card tile, card modal. |
| `features/collection` | Collection view, quantity editor, bulk-add dialog, value sparkline. |
| `features/decks` | Deck list, deck builder, card quantity management. |
| `features/admin` | User management table, import UI, dry-run preview. |
| `features/api` | Typed API client functions (grouped by resource). |
| `features/printings` | Card printing detail component. |
| `components/ui` | shadcn/ui-based primitives (`Select`, `Button`, `Dialog`, …). |
| `components/filters` | `FilterDropdown` — reusable accessible filter wrapper. |
| `components/charts` | `LineSparkline` — 30-day value trend chart. |
| `lib/http.ts` | Axios instance with auth header injection. |
| `lib/queryKeys.ts` | Centralized TanStack Query key factory. |
| `state/UserContext.tsx` | Global auth state (current user, token). |

---

## Card Import System

Each importer lives in `api/Importing/<Game>Importer.cs` and implements `ICardImporter`.

- Importers are registered in `api/Program.cs` and invoked via `POST /api/admin/import`.
- All imports support **dry-run mode** (`dryRun: true`) — returns a preview without persisting.
- Upsert logic ensures **idempotency** — re-running an import never creates duplicates.
- See `ADMIN.md` for expected file formats and import workflow.

**Existing importers:** MTG (Scryfall), Pokémon TCG API, Lorcana (LorcanaDB), Flesh and Blood (FABDB), Star Wars Unlimited (SWUDB), Star Wars CCG (SWCCGDB), Guardians, Dicemasters, Transformers, and several others.