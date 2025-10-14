# Copilot Instructions

## Setup
- Requires **.NET 9 SDK** (per `global.json`) and **Node.js 20+`.
- API
  ```bash
  dotnet restore ./api/api.csproj
  dotnet build ./api/api.csproj
  dotnet run --project ./api/api.csproj
  ```
- Client
  ```bash
  pnpm install --filter client-vite...
  pnpm --filter client-vite dev
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