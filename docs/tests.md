# API Integration Tests

## In-memory SQLite test host
- `api.Tests/Infrastructure/TestingWebAppFactory` configures the API for integration tests using a single shared `SqliteConnection` opened against `Filename=:memory:`.
- The factory replaces the normal `AppDbContext` registration with `UseSqlite(sharedConnection)` and builds the schema with `Database.EnsureCreated()`.
- `Program.cs` skips production migrations and seeding when `ASPNETCORE_ENVIRONMENT` is `Testing`, allowing the test project to manage data explicitly.
- `Seed.SeedAsync` populates a deterministic multi-user dataset that exercises collections, wishlists, decks, and availability rules.

## Running the tests
- From the command line run `dotnet test` at the repository root to execute the full suite (this is what CI uses).
- In Visual Studio 2022 open the solution and run tests via **Test Explorer**; the integration fixtures light up automatically.

## Adding new API tests
- Create test classes under `api.Tests/<Area>` and depend on `TestingWebAppFactory` via `IClassFixture`.
- Call `ResetStateAsync` followed by `ExecuteDbContextAsync(Seed.SeedAsync)` (or a custom seeder) at the start of each test to ensure isolation.
- Use `CreateClientForUser(userId)` to obtain an `HttpClient` with the required `X-User-Id` header.
- Prefer strongly typed DTO contracts (records) and `HttpClient` JSON helpers for assertions; `FluentAssertions` is available for expressive checks.
