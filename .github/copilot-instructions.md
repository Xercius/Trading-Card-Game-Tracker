# Copilot Instructions

## Project Overview
A local web application for tracking trading card collections, deck building, wishlists, and collection/deck values. Supports **Magic the Gathering**, **Star Wars Unlimited**, **Disney Lorcana**, **Flesh and Blood**, **Star Wars CCG**, **Guardians**, **Dicemasters**, **Pokémon TCG**, and **Transformers**. Cards are imported from external APIs/scrapers via 13 game-specific importers in `api/Importing/`.

## Setup
- Requires **.NET 9 SDK** (per `global.json`) and **Node.js 20+**.
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

## Coding Standards
### C#
- Prefer async/await. Inject dependencies via DI.
- Small, single-purpose services. Records/immutable DTOs.
- Controllers return `ActionResult<T>` and RFC 7807 `ProblemDetails`.
- Case-insensitive search: avoid `ToLower()`. Use SQLite NOCASE or `EF.Functions.Like` with normalized columns.
- Map only via AutoMapper profiles.

### TypeScript/React
- Functional components + hooks. Strict typing (`noImplicitAny`).
- TanStack Query for server state. shadcn/ui + Tailwind for UI.
- One component per file. No side effects in render.

### Testing
- API: xUnit + WebApplicationFactory with SQLite (in-memory or file per test).
- Client: Vitest + React Testing Library.
- Deterministic tests. Assert HTTP codes and JSON shapes.
- Run backend tests: `ASPNETCORE_ENVIRONMENT=Testing dotnet test ./api/api.sln -c Release`
- Run frontend tests: `cd client-vite && npm test -- --run`

## Security
- Trust forwarded headers only from configured `KnownProxies`/`KnownNetworks` within `ForwardLimit`.
- Resolve client IP from rightmost `X-Forwarded-For` that is not a trusted proxy.
- Never authorize based on `Host` header.
- Secrets only via env or configuration files ignored by Git.

## Pull Requests
- One concern per PR. Keep diffs small.
- Describe commands used for testing and expected results.
- Run all tests locally before push.
- No route or schema changes without migration and note.

## Style / Formatting
- Follow `.editorconfig` and Prettier. 4-space (C#), 2-space (TS/JSON).
- `dotnet format` and `pnpm format` before commit.
- Keep imports ordered. Remove unused.

---

## Architectural Patterns

### C# API
- **Feature folders:** `/Features/<Area>/{Controller, Dtos, Mapping, Services, Validation}`. No cross-feature coupling.
- **Guard clauses:** Early return on invalid input. Small private helpers over nested `if`.
- **Cancellation:** Pass `CancellationToken` from controller to EF.
- **Result shape:** Use 200/201/204/400/404/409. RFC 7807 for errors.
- **Idempotent imports:** `/api/admin/import` uses checksums/upserts. No duplicate rows on retry.
- **Time handling:** UTC-only persistence with `DateTimeOffset` or UTC `DateTime`.
- **Options pattern:** `IOptions<T>` + `ValidateOnStart()` for config sections.
- **Logging:** Structured logs with `EventId`. Use `BeginScope` for request/deck/user context.

### EF Core
- **Read vs Write:** Reads use `.AsNoTracking()`. Writes track entities.
- **Paging:** All list endpoints accept `page` and `pageSize` (max cap). Return `X-Total-Count`.
- **Search:** Use `EF.Functions.Like(col, pattern)` or NOCASE collation; avoid `ToLower()` to keep indexes.
- **Projection:** Prefer `Select` into DTOs over `Include`. Use `.AsSplitQuery()` when needed.
- **Indexes:** Add indexes for lookups and normalized text columns.
- **Transactions:** `BeginTransactionAsync()` for multi-entity changes.
- **Concurrency:** Use `RowVersion`/concurrency tokens on mutable aggregates.

### Validation & Errors
- **FluentValidation:** Per-request validators in `/Features/*/Validation`.
- **Problem factory:** Central `ProblemDetailsFactory` maps known exceptions â 400/404/409 with consistent type/instance/title.
- **Model errors:** Return validation summary in `errors` extension per RFC 7807.

### Security Patterns
- **Current user:** `ICurrentUser` abstraction from claims. Controllers do not parse headers directly.
- **Forwarded headers:** Rightmost non-trusted-proxy IP from `X-Forwarded-For` within limit.

### HTTP API Conventions
- **Routes:** `/api/<plural>` with REST verbs. No verbs in paths.
- **Created:** `POST` returns `201 Created` with `Location` header.
- **ETags (optional):** Heavy GETs may use ETags. Honor `If-None-Match`.

### Testing Patterns
- **API:** One test = one HTTP call/assert status + JSON schema. Use factory and per-test DB.
- **EF:** Wrap tests in transaction and rollback. Seed with minimal fixtures.
- **Time:** Fixed clock via `ISystemClock` or test shim.

### React/TypeScript
- **Query keys:** Centralized in `client-vite/src/lib/queryKeys.ts`, e.g. `['cards', { game, set, page, q }]`.
- **React Query:** Configure `staleTime`. Use `select` to shape data. Optimistic updates with `onMutate/onError/onSettled`.
- **Schemas:** `zod` for request/response validation. Consider generating types from OpenAPI/DTOs.
- **Forms:** `react-hook-form` + `@hookform/resolvers/zod`. No ad-hoc uncontrolled state.
- **Routing:** Per-feature route modules with `Suspense` and `ErrorBoundary` per route.
- **Virtualization:** `@tanstack/react-virtual` for card grids.
- **State rule:** Server state in React Query. Local UI state in components. Avoid global state unless cross-route.

### Styling/UI
- **shadcn/ui:** Use local registry. Variants via `cva`. Minimal inline styles.
- **A11y:** Keyboard reachability and proper `aria-*` on custom controls.

### Build/CI
- **Gates:** `dotnet format --verify-no-changes`, `pnpm lint`, `pnpm typecheck`, and tests in CI.
- **Migrations:** Every model change includes a named migration plus snapshot diff in PR.

---

## Data Model (EF Core / SQLite)

| Entity | Purpose |
|--------|---------|
| `Card` | Unique card definition — `(Game, Name)` unique. |
| `CardPrinting` | Printing variant per set/number/image. FK → `Card`. |
| `User` | Registered user with `Email`, `Role` (`User`/`Admin`), timestamps. |
| `UserCard` | Owned inventory — `(UserId, CardPrintingId)` unique; `Quantity`, `IsProxy`. |
| `Deck` | Named deck belonging to a `User`. |
| `DeckCard` | Deck membership — `(DeckId, CardPrintingId)` unique; `Quantity`. |
| `WishlistEntry` | Desired cards — `(UserId, CardPrintingId)` unique; `DesiredQuantity`. |
| `CardPriceHistory` | Per-printing price at a point in time. `(CardPrintingId, CapturedAt)` unique. |
| `ValueHistory` | Aggregated daily value snapshots for a user's collection or deck. |

**Key rules:**
- Proxy cards (`IsProxy=true`) are **excluded** from all value calculations.
- All timestamps are stored as UTC (`DateTimeOffset` or UTC `DateTime`).

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

---

## Common Patterns to Follow

### New API endpoint
1. Add controller method in the relevant `Features/<Area>/<Area>Controller.cs`.
2. Create request/response DTOs as `record` types in `Features/<Area>/Dtos/`.
3. Add FluentValidation validator in `Features/<Area>/Validation/`.
4. Add AutoMapper profile in `Features/<Area>/Mapping/`.
5. Wire service logic in `Features/<Area>/Services/` (inject via DI).
6. Add integration test in `api.Tests/Features/<Area>/`.

### New React page / feature
1. Create page component in `client-vite/src/pages/`.
2. Add route in `client-vite/src/routes/index.tsx` with `Suspense` + `ErrorBoundary`.
3. Add API function in `client-vite/src/features/api/`.
4. Register query key in `client-vite/src/lib/queryKeys.ts`.
5. Use `useQuery`/`useMutation` from TanStack Query in the component.
6. Add Vitest test in a `__tests__/` subdirectory next to the component.