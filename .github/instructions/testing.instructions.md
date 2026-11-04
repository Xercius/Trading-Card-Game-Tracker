---
applyTo: "api.Tests/**,client-vite/src/**/__tests__/**,client-vite/src/**/*.test.{ts,tsx}"
---

# Testing

## Backend (xUnit)
- Use `WebApplicationFactory<Program>` with one isolated SQLite DB per test (see `api.Tests/Fixtures/`, `Helpers/`) and `ASPNETCORE_ENVIRONMENT=Testing`. No external network calls: stub `HttpMessageHandler`.
- Names: `<Endpoint>_<Scenario>_<Outcome>`. Assert status code and JSON shape, not internals. Prefer the real DB over mocks.
- Every new endpoint needs tests for success, 400, 404, 409 (if uniqueness applies) and 403 (if admin-only).
- Every SWU importer/sync needs a run-twice-no-duplicates test and a `dryRun` test.
- Time-dependent code uses an injected `TimeProvider`; never call `DateTime.Now`/`UtcNow` in code under test.
- Single class: `ASPNETCORE_ENVIRONMENT=Testing dotnet test ./api/api.sln -c Release --filter "FullyQualifiedName~<TestClassName>"`.

## Frontend (Vitest + React Testing Library)
- Place tests in `__tests__/` next to the unit, named `<Name>.test.tsx` / `<hook>.test.ts`.
- Query by role, then label, then text; use `getByTestId` only as a last resort.
- Mock network with `msw`; wrap components in a test `QueryClientProvider`; use `renderHook` with wrappers for hooks.
