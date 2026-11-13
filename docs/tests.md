# API Integration Tests

## In-memory SQLite test host

- `api.Tests/Infrastructure/TestingWebAppFactory` configures the API for integration tests using a single shared `SqliteConnection` opened against `Filename=:memory:`.
- The factory replaces the normal `AppDbContext` registration with `UseSqlite(sharedConnection)` and builds the schema with `Database.EnsureCreated()`.
- `Program.cs` skips production migrations and seeding when `ASPNETCORE_ENVIRONMENT` is `Testing`, allowing the test project to manage data explicitly.
- `Seed.SeedAsync` populates a deterministic single-user dataset (`UserId = 1`, `Username = "owner"`) along with sample cards, printings, collections, wishlists, and decks.

## Running the tests

- From the command line run `ASPNETCORE_ENVIRONMENT=Testing dotnet test ./api/api.sln -c Release` at the repository root to execute the full suite (matching CI).
- In Visual Studio open the solution and run tests via **Test Explorer**; the integration fixtures light up automatically.

## Adding new API tests

- Create test classes under `api.Tests/<Area>` and depend on `TestingWebAppFactory` via `IClassFixture`.
- Call `ResetStateAsync` followed by `ExecuteDbContextAsync(Seed.SeedAsync)` (or a custom seeder) at the start of each test to ensure isolation.
- Use `CreateClientForUser(userId)` to obtain a standard test `HttpClient` (single-user mode), and call `.AsAdmin()` on the client to attach the admin `Bearer` token for admin endpoints.
- Prefer strongly typed DTO contracts (records) and `HttpClient` JSON helpers for assertions; `FluentAssertions` is available for expressive checks.
