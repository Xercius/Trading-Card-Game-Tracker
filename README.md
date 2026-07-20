As a home project and learning experience I am building a web app to run locally on my PC to track my trading card collections. This will also include tracking deck construction, wish lists, and hopefully collection/deck values.
The goal is to eventually incorporate cards from the following games:
--Magic the Gathering
--Star Wars Unlimited
--Disney Lorcana
--Flesh and Blood
--Star Wars CCG
--Guardians
--Dicemasters
--Pokémon TCG
--Transformers

## Development Environment
- **.NET SDK**: 10.0 (pinned via `global.json`)
- **Node.js**: 20+ (for Vite client)
- **Entity Framework Core**: 10.0.10 (Sqlite provider, design, and tools packages)

### Running the Development Servers

#### API Server
From the repository root:
```bash
dotnet restore ./api/api.csproj
dotnet build ./api/api.csproj
dotnet run --project ./api/api.csproj --launch-profile "TradingCardApi (HTTPS)"
```
The API will run on `https://localhost:7226` and `http://localhost:5229`.

To verify the API is running, access the health endpoint:
```bash
curl -k https://localhost:7226/api/health
```

#### Vite Development Server
From the repository root:
```bash
pnpm install --filter client-vite...
pnpm --filter client-vite dev
```
The Vite dev server will run on `http://localhost:5173`.

#### Client Configuration
Create a `.env.local` file in the `client-vite` directory (already gitignored):
```bash
# Point directly to the API server (recommended - no proxy)
VITE_API_BASE=https://localhost:7226/api
```

This configures the client to make requests directly to the API server. Alternatively, you can copy the provided `.env.example` file:
```bash
cd client-vite
cp .env.example .env.local
```

To verify the API is reachable, access the health endpoint:
```bash
curl -k https://localhost:7226/api/health
```

### JWT configuration
- In production (or any deployed environment), provide a 256-bit or longer signing key via the `JWT__KEY` environment variable. The API refuses to start if the key is missing or too short.
- For local development you can optionally store a key with `dotnet user-secrets`:
  ```bash
  dotnet user-secrets set "Jwt:Key" "DevOnly_Minimum_32_Chars_Key_For_Local_Use_1234" --project ./api/api.csproj
  ```
  When no key is supplied in Development/Testing the API falls back to a deterministic development key and logs a warning on startup.

### Minimal developer seed data
- Ensure you are in the `api` directory, then run `dotnet run seed` to populate the SQLite database with three sample games and sets for UI testing. The command runs migrations first, skips if any cards already exist, and exits without starting the web server.

## API overview

### Cards
- `GET /api/cards` – paged, filtered card search (supports `game`, `set`, `rarity`, `q` query parameters).
- `GET /api/cards/search` – alias for the main search endpoint.
- `GET /api/cards/{id}` – retrieve a single card by ID.
- `GET /api/cards/{id}/printings` – returns the available printings for a card, ordered by set and collector number.
- `GET /api/cards/{id}/sparkline?days=30` – returns aggregated value points for the card across its non-proxy printings.
- `POST /api/cards/printing` – add a new card printing.
- `POST /api/cards/{id}/printings/import` – import printings for a card.
- `GET /api/cards/printings` – list all printings.
- `GET /api/cards/facets` – all filter facets (games, sets, rarities) with counts.
- `GET /api/cards/facets/games` – distinct game names.
- `GET /api/cards/facets/sets` – distinct set codes, optionally filtered by `game`.
- `GET /api/cards/facets/rarities` – distinct rarity values.

### Collection
- `GET /api/collection` – list the authenticated user's owned cards.
- `POST /api/collection/items` – quick-add endpoint that increments the owned quantity for the authenticated user.
- `POST /api/collection` – add a card to the collection.
- `PUT /api/collection/{cardPrintingId}` – replace quantity for a collection entry.
- `PATCH /api/collection/{cardPrintingId}` – partially update a collection entry.
- `PATCH /api/collection/bulk` – bulk-update quantities for multiple entries.
- `POST /api/collection/delta` – apply a quantity delta to a collection entry.
- `DELETE /api/collection/{cardPrintingId}` – remove a card from the collection.
- `GET /api/collection/value/history?days=90` – produces daily totals for the authenticated user's collection with proxies excluded.

### Decks
- `GET /api/decks` – list decks for the authenticated user.
- `POST /api/deck` – create a new deck.
- `GET /api/deck/{deckId}` – retrieve a specific deck.
- `PATCH /api/deck/{deckId}` – partially update a deck (e.g. rename).
- `PUT /api/deck/{deckId}` – fully update a deck.
- `DELETE /api/deck/{deckId}` – delete a deck.
- `GET /api/deck/{deckId}/cards` – list cards in a deck.
- `GET /api/deck/{deckId}/cards-with-availability` – list cards with owned-quantity availability.
- `POST /api/deck/{deckId}/cards` – add a card to the deck.
- `POST /api/deck/{deckId}/cards/upsert` – upsert a card in the deck.
- `POST /api/deck/{deckId}/cards/delta` – apply a quantity delta to a deck card.
- `POST /api/deck/{deckId}/cards/quantity-delta` – alias for quantity delta.
- `PUT /api/deck/{deckId}/cards/{cardPrintingId}` – replace quantity for a deck card.
- `PATCH /api/deck/{deckId}/cards/{cardPrintingId}` – partially update a deck card.
- `DELETE /api/deck/{deckId}/cards/{cardPrintingId}` – remove a card from the deck.
- `GET /api/deck/{deckId}/availability` – summarise owned vs. required quantities.
- `GET /api/decks/{deckId}/value/history?days=90` – daily deck values across owned printings with proxies excluded.

### Wishlist
- `GET /api/wishlist` – list the authenticated user's wishlist entries.
- `POST /api/wishlist/items` – quick-add endpoint that increments the desired quantity for the authenticated user.
- `POST /api/wishlist` – add a card to the wishlist.
- `PUT /api/wishlist` – update a wishlist entry.
- `POST /api/wishlist/move-to-collection` – move a wishlist entry into the collection.
- `DELETE /api/wishlist/{cardPrintingId}` – remove a card from the wishlist.

### Prices & values
- `GET /api/prices/{printingId}/history` – price history for a specific printing.
- `GET /api/value/collection/summary` – aggregated collection value summary for the authenticated user.
- `GET /api/value/cardprinting/{id}` – current value for a specific card printing.
- `GET /api/value/deck/{deckId}` – current value for a specific deck.
- `POST /api/value/refresh` – trigger a value recalculation.

### Health
- `GET /api/health` – returns a simple health check status to verify API connectivity.

### Users
- `GET /api/user/{id}` – retrieve a user by ID.

## Admin operations
- `/api/admin/import/options` – returns the available importers, supported games, and known set codes.
- `/api/admin/import/dry-run` – preview an import without persisting changes (JSON payload or `multipart/form-data`).
- `/api/admin/import/apply` – commit an import. Mirrors the dry-run payloads.
- `/api/admin/prices/ingest` – ingest per-printing price snapshots; upserts on `(cardPrintingId, capturedAt)`.

See [ADMIN.md](ADMIN.md) for the full admin import guide including file formats and the example workflow.
