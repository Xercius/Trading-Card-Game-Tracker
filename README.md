# Trading Card Game Tracker

As a home project and learning experience I am building a web app to run locally on my PC to track my trading card collections. This will also include tracking deck construction, wish lists, and hopefully collection/deck values.
The goal is start with Star Wars Unlimited, then expand to Star Wars CCG and then to eventually incorporate cards from the following games:
--Magic the Gathering
--Hellbreak TCG
--Disney Lorcana
--Flesh and Blood
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

cd client-vite
npm ci
npm run dev

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

### Single-owner local access

The local web app opens directly as the seeded owner (`owner`, `DbSeeder.DefaultUserId`); no regular site login or owner password is required. Collection, deck, wishlist, and value views always use this owner. SWU Sync has a separate administrator sign-in and authorization flow.

### Minimal developer seed data

- Ensure you are in the `api` directory, then run `dotnet run seed` to run EF Core migrations, ensure the default `owner` user (`Id = 1`) exists, and exit without starting the web server.

## API overview

### Cards

- `GET /api/cards` – virtualized, filtered card search (`skip`, `take`, `includeTotal`, plus `game`, `set`, `rarity`, `q`, `type`, `arena`, `aspect`, `aspectMethod`, `trait`, `traitMethod`, `keyword`, `keywordMethod`).
- `GET /api/cards/search` – paged card search (`page`, `pageSize`, `name`, `includePrintings`, plus the filter parameters above).
- `GET /api/cards/{id}` – retrieve a single card by ID.
- `GET /api/cards/{id}/printings` – returns the available printings for a card, ordered by set and collector number.
- `GET /api/cards/{id}/sparkline?days=30` – returns aggregated value points for the card across its non-proxy printings.
- `POST /api/cards/printing` – add a new card printing (Admin only).
- `POST /api/cards/{id}/printings/import` – import printings for a card (Admin only).
- `GET /api/cards/printings` – list printings with full game-specific filters.
- `GET /api/cards/facets` – all filter facets (`games`, `sets`, `rarities`, `types`, `arenas`, `aspects`, `traits`, `keywords`, `treatments`, and SWCCG/Pokémon facets) with counts.
- `GET /api/cards/facets/games` – distinct game names.
- `GET /api/cards/facets/sets` – distinct set names (falling back to codes), optionally filtered by `game`.
- `GET /api/cards/facets/rarities` – distinct rarity values.

### Collection

- `GET /api/collection` – list the owner's owned cards.
- `GET /api/collection/{cardPrintingId}` – get owned/proxy/wanted quantities for a single printing.
- `GET /api/collection/sets-summary` – completion summary grouped by set.
- `POST /api/collection/items` – quick-add endpoint that increments the owner's quantity.
- `POST /api/collection` – add a card to the collection.
- `POST /api/collection/batch-set` – batch-set quantities for multiple printings.
- `PUT /api/collection/{cardPrintingId}` – replace quantity for a collection entry.
- `PATCH /api/collection/{cardPrintingId}` – partially update a collection entry.
- `PATCH /api/collection/bulk` – bulk-update quantities for multiple entries.
- `POST /api/collection/delta` – apply a quantity delta to a collection entry.
- `DELETE /api/collection/{cardPrintingId}` – remove a card from the collection.
- `GET /api/collection/value/history?days=90` – produces daily totals for the owner's collection with proxies excluded.

### Decks

Decks for all games can include cards you do not own or quantities above your collection counts. Adding a card to a deck does not add it to your collection. Availability is shown separately as the remaining owned copies after deck quantities, clamped at zero; "Include proxies" only changes this availability readout, not which cards can be added. Cards must still match the deck's game.

SWU deck building enables "In-aspect only" after the required leader(s) and base are selected. This filter checks every required aspect symbol before pagination, so page counts and result totals reflect the matching cards. The printings API accepts an optional `availableAspects` CSV budget (repeat names for multiple symbols) independently of the ordinary aspect filters.
Printing-list responses include `cost`, `subtitle`, and `destiny` from the associated shared `Card`; these remain card-level data, not separate printing-level storage.

- `GET /api/decks` – list the owner's decks.
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
- `GET /api/deck/{deckId}/validation` – validate deck construction rules (e.g., Pokémon TCG).
- `GET /api/decks/{deckId}/value/history?days=90` – daily deck values across owned printings with proxies excluded.

### Wishlist

- `GET /api/wishlist` – list the owner's wishlist entries.
- `POST /api/wishlist/items` – quick-add endpoint that increments the owner's desired quantity.
- `POST /api/wishlist` – add a card to the wishlist.
- `PUT /api/wishlist` – bulk-set wishlist entries (`IEnumerable<BulkSetWishlistRequest>`).
- `POST /api/wishlist/move-to-collection` – move a wishlist entry into the collection.
- `DELETE /api/wishlist/{cardPrintingId}` – remove a card from the wishlist.

### Import & Export

- `GET /api/export/json` – export collection, wishlist, and decks as JSON.
- `GET /api/export/collection.csv` – export collection as CSV.
- `GET /api/export/wishlist.csv` – export wishlist as CSV.
- `GET /api/export/decks.csv` – export decks as CSV.
- `POST /api/import/json` – import user data from a JSON export.

### Prices & values

- `GET /api/prices/{printingId}/history` – price history for a specific printing.
- `GET /api/value/collection/summary` – aggregated collection value summary for the owner.
- `GET /api/value/cardprinting/{id}` – current value for a specific card printing.
- `GET /api/value/deck/{deckId}` – current value for a specific deck.
- `POST /api/value/refresh` – trigger a value recalculation.

### Health

- `GET /api/health` – returns a simple health check status to verify API connectivity.

### Users

- `GET /api/user/{id}` – retrieve a user by ID.

## Admin operations

- `GET / POST / DELETE /api/admin/sync/session` – check, create, or clear the HttpOnly `AdminSession` cookie.
- `GET /api/admin/import/options` – returns the available importers, supported games, and known set codes.
- `GET /api/admin/import/history` – returns recent remote import checkpoint history.
- `POST /api/admin/import/dry-run` – preview an import without persisting changes (JSON payload or `multipart/form-data`).
- `POST /api/admin/import/apply` – commit an import. Mirrors the dry-run payloads.
- `POST /api/admin/sync/star-wars-unlimited` (plus `GET .../status` and `GET .../logs`) – sync the SWU catalog incrementally or with `?forceFullSync=true`.
- `POST /api/admin/sync/star-wars-ccg` (plus `GET .../status` and `GET .../logs`) – sync the Star Wars CCG catalog.
- `GET /api/admin/sync/pokemon/status`, `POST /api/admin/sync/pokemon/sets`, `POST /api/admin/sync/pokemon/cards`, `GET /api/admin/sync/pokemon/logs` – sync Pokémon TCG (TCGdex) sets/cards and inspect status/logs.
- `POST /api/admin/prices/ingest` – ingest per-printing price snapshots; upserts on `(cardPrintingId, capturedAt)`.

## Configuration

Backend configuration sections in `appsettings*.json` / environment variables / user-secrets include:

- `Cors` (`PolicyName`, `Origins`, `Headers`, `Methods`, `AllowCredentials`, `PreflightMaxAge`)
- `ForwardedHeaders` (`ForwardedHeaders`, `ForwardLimit`, `KnownProxies`, `KnownNetworks`)
- `Admin:ApiToken` / `Admin:SessionSigningKey` / `Admin:SessionCookieName` (see [ADMIN.md](ADMIN.md))

See [ADMIN.md](ADMIN.md) for the full admin import guide including file formats and the example workflow.
The SWU admin import and sync workflows update the shared `Card` and `CardPrinting` tables used by collections, decks, and wishlists. See the [SWU import guide](docs/SWU_IMPORT_GUIDE.md) for details.
