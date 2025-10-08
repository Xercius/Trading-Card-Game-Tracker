As a home project and learning experience I am building a web app to run locally on my PC to track my trading card collections. This will also include tracking deck construction, wish lists, and hopefully collection/deck values.
The goal is to eventually incorporate cards from the following games:
--Magic the Gathering
--Star Wars Unlimited
--Disney Lorcana
--Flesh and Blood
--Star Wars CCG
--Guardians
--Dicemasters

## Development Environment
- **.NET SDK**: 8.0 (pinned via `global.json`)
- **Entity Framework Core**: 9.0.9 (Sqlite provider, design, and tools packages)

### Minimal developer seed data
- Ensure you are in the `api` directory, then run `dotnet run seed` to populate the SQLite database with three sample games and sets for UI testing. The command runs migrations first, skips if any cards already exist, and exits without starting the web server.

## Recent API additions
- `GET /api/cards/{id}/printings` – returns the available printings for a card, ordered by set and collector number.
- `GET /api/cards/{id}/sparkline?days=30` – returns aggregated value points for the card across its non-proxy printings.
- `GET /api/collection/value/history?days=90` – produces daily totals for the authenticated user's collection with proxies excluded.
- `GET /api/decks/{deckId}/value/history?days=90` – surfaces daily deck values across owned printings with proxies excluded.
- `POST /api/collection/items` – quick-add endpoint that increments the owned quantity for the authenticated user.
- `POST /api/wishlist/items` – quick-add endpoint that increments the desired quantity for the authenticated user.

## Admin operations
- `[RequireAdmin]` enforces administrator access based on the `X-User-Id` middleware context and returns a `403` problem response otherwise.
- `/api/admin/users` exposes admin-only CRUD operations for users with optimistic UI support in the Vite client. The API prevents demoting or deleting the final administrator and returns a `409 Conflict` problem response when that safeguard triggers.
- `/api/admin/prices/ingest` ingests per-printing price points and safely upserts duplicates based on `(cardPrintingId, capturedAt)`.
