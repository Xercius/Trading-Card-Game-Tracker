---
applyTo: "api.Tests/**,client-vite/src/**/__tests__/**,client-vite/src/**/*.test.{ts,tsx}"
---

# Testing

## Backend (xUnit)
- Use `WebApplicationFactory<Program>` with one isolated SQLite DB per test (see `api.Tests/Fixtures/`, `Helpers/`) and `ASPNETCORE_ENVIRONMENT=Testing`. No external network calls: stub `HttpMessageHandler`.
- Names: `<Endpoint|Service>_<Scenario>_<Outcome>` (e.g. `SwuImporter_RunTwice_NoDuplicates`). Assert on HTTP status code, response JSON shape, and (when relevant) persisted DB state via the test DbContext. Do not assert on private methods, call counts, or implementation details.
- Every new endpoint needs tests for success, 400 (if it accepts a body or validated parameters), 404 (if it addresses a resource by id), 409 (if uniqueness applies) and 403 (if admin-only).
- Importer/sync tests must verify that replaying the same source data creates no duplicates; verify dry-run persistence and checkpoint behavior only for operations that expose `dryRun`.
- Time-dependent code uses an injected `TimeProvider`; never call `DateTime.Now`/`UtcNow` in code under test.
- Single class: `ASPNETCORE_ENVIRONMENT=Testing dotnet test ./api/api.sln -c Release --filter "FullyQualifiedName~<TestClassName>"`.

## Frontend (Vitest + React Testing Library)
- Place tests in `__tests__/` next to the unit, named `<Name>.test.tsx` / `<hook>.test.ts`.
- Query by role, then label, then text; use `getByTestId` only as a last resort.
- Mock network with `msw`; wrap components in a test `QueryClientProvider`; use `renderHook` with wrappers for hooks.
