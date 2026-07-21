---
applyTo: "api/Migrations/**,api/Data/**,api/Models/**"
---

# Database Guidelines

## Data Model Reference

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
- All timestamps are stored as UTC (`DateTimeOffset` or UTC `DateTime`). Never persist local time.

## EF Core Configuration

### DbContext
- Use `ApplicationDbContext` (in `api/Data/`) as the single EF Core entry point.
- Register entities via `OnModelCreating`. Use `IEntityTypeConfiguration<T>` classes in `api/Data/Configuration/` for complex models.
- Never put business logic inside `DbContext`. Keep it as a pure data access layer.

### Migration Standards
- Every model change requires a named `dotnet ef migrations add <DescriptiveName>` migration.
- Migration names must describe the schema change, e.g., `AddCardPrintingSetIndex`, `AddWishlistDesiredQuantity`.
- Include the migration snapshot diff (`<Context>ModelSnapshot.cs`) in the PR.
- Review generated SQL (`dotnet ef migrations script`) before merging — confirm no data loss.
- Never delete or modify existing migration files after they have been applied to any environment.
- For breaking column changes (type changes, renames), create an explicit migration with a data migration step.

### Applying Migrations
```bash
# Apply all pending migrations
dotnet ef database update --project api/api.csproj

# Generate SQL script for review
dotnet ef migrations script --project api/api.csproj --output migration.sql
```

## Index Guidelines

- Add indexes on all foreign key columns (EF does not add them automatically for SQLite in some configurations).
- Add indexes on columns used in `WHERE`, `ORDER BY`, or `EF.Functions.Like` patterns.
- Use `HasIndex(...).IsUnique()` for uniqueness constraints (e.g., `(UserId, CardPrintingId)`).
- Prefer composite indexes when queries always filter on the same combination of columns.
- Do not add indexes speculatively — confirm with a realistic query plan.

### Naming Convention
- Index names: `IX_<TableName>_<ColumnName(s)>`, e.g., `IX_CardPrintings_CardId`, `IX_UserCards_UserId_CardPrintingId`.
- Unique constraint names: `UX_<TableName>_<ColumnName(s)>`.

## Query Performance Guidelines

- All reads use `.AsNoTracking()` unless the entity will be modified.
- Use `Select` projections into DTOs instead of loading full entities with `Include` chains.
- Use `.AsSplitQuery()` when a query results in a Cartesian product from multiple collection includes.
- Cap all list queries at a maximum `pageSize` (100 rows) to prevent unbounded fetches.
- Avoid N+1 queries — always check whether a loop contains EF calls and refactor with `Include` or a batch load.
- Use `EF.Functions.Like(col, pattern)` for text search; never call `ToLower()` in LINQ as it bypasses SQLite NOCASE indexes.

## Data Integrity Rules

- Foreign key constraints are enforced at the model level via EF Core relationships.
- Soft deletes are not currently used — deletes are permanent. Consider audit implications before adding cascade deletes.
- Unique constraints are enforced at the database level (not just application level) for entities with composite keys.
- `IsProxy = true` on `UserCard` excludes that card from all value aggregation. Always filter `WHERE IsProxy = 0` in value queries.
- `DesiredQuantity` on `WishlistEntry` must be a positive integer; validate at the service layer before persisting.

## SQLite-Specific Notes

- SQLite stores booleans as integers (0/1). EF handles this automatically but be aware in raw SQL.
- SQLite `TEXT` columns with `COLLATE NOCASE` support case-insensitive comparisons without calling `ToLower()`.
- SQLite does not enforce foreign keys by default — EF Core enables them via `PRAGMA foreign_keys = ON` at connection open time. Verify this is enabled in `ApplicationDbContext`.
- Avoid schema changes that require SQLite column type alterations — SQLite does not support `ALTER COLUMN`. Use a new migration that recreates the table.

## Environment & Seeding

- Development seed data is loaded via `cd api && dotnet run seed`.
- Tests use a per-test SQLite database (in-memory or file) created by `WebApplicationFactory`.
- Never seed production data via application startup code; use explicit migration data seeds instead.
- Keep seed data minimal and deterministic for tests (fixed IDs, stable names).
