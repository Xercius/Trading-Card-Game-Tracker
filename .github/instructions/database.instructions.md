---
applyTo: "api/Data/**,api/Migrations/**,api/Models/**"
---

# Database (EF Core + SQLite)

- Single context: `AppDbContext` (`api/Data/`). No business logic in it; use `IEntityTypeConfiguration<T>` for complex models.
- Unique constraints are enforced in the DB: `UserCard(UserId,CardPrintingId)`, `DeckCard(DeckId,CardPrintingId)`, `WishlistEntry(UserId,CardPrintingId)`, `CardPriceHistory(CardPrintingId,CapturedAt)`, `Set(GameId,Code)`, `Card(Game,Name)`.
- Create migrations with `dotnet ef migrations add <DescriptiveName> --project api/api.csproj`; commit the snapshot. Never edit applied migrations.
- Indexes: add for foreign keys and for columns used in filters/sorts; none speculatively. Names: `IX_<Table>_<Cols>`, `UX_<Table>_<Cols>`.
- SQLite cannot `ALTER COLUMN`; use table-rebuild migrations with an explicit data step. Verify `PRAGMA foreign_keys = ON` stays enabled.
- Value queries must filter `IsProxy = false`. `WishlistEntry.DesiredQuantity` must be > 0 (validated in the service).
- Deletes are hard (no soft delete); check cascade effects before adding any.
- Timestamps are UTC (`DateTimeOffset`).
- Never seed production data from startup code. Dev seed: `cd api && dotnet run seed`.
