---
applyTo: "api/Data/**,api/Migrations/**,api/Models/**"
---

# Database (EF Core + SQLite)

- Single context: `AppDbContext` (`api/Data/`). No business logic in it; use `IEntityTypeConfiguration<T>` for complex models.
- Unique constraints are enforced in the DB: `UserCard(UserId,CardPrintingId)`, `DeckCard(DeckId,CardPrintingId)`, `WishlistEntry(UserId,CardPrintingId)`, `CardPriceHistory(CardPrintingId,CapturedAt)`, `Set(GameId,Code)`, `Card(Game,Name)`.
- Create migrations with `dotnet ef migrations add <DescriptiveName> --project api/api.csproj`; commit the snapshot. Never read, review, or manually edit `*.Designer.cs` or `AppDbContextModelSnapshot.cs`. Never edit a migration once it has been committed/merged to the main branch. For a migration that only exists on your local branch, remove it with `dotnet ef migrations remove` and recreate it instead of editing it in place.
- Indexes: add for foreign keys and for columns used in filters/sorts; none speculatively. Names: `IX_<Table>_<Cols>`, `UX_<Table>_<Cols>`.
- SQLite cannot `ALTER COLUMN`; use table-rebuild migrations with an explicit data step. Verify `PRAGMA foreign_keys = ON` stays enabled.
- Any query that sums or aggregates card prices (collection value, deck value, price history totals) must exclude proxies with `.Where(uc => !uc.IsProxy)`; general listing queries must still return proxies. `WishlistEntry.DesiredQuantity` must be > 0 (validated in the service).
- Deletes are hard (no soft delete). Before adding a new foreign key relationship or a delete endpoint, review the configured `OnDelete` behavior and list which child rows will be removed. New relationships default to `DeleteBehavior.Restrict`; use `Cascade` only for true owned children (e.g., `DeckCard` under `Deck`) and document it in the entity configuration.
- Timestamps are UTC (`DateTimeOffset`).
- Never seed production data from startup code. Dev seed: `cd api && dotnet run seed`.
