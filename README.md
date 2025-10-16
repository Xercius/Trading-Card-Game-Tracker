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
- **.NET SDK**: 9.0 (pinned via `global.json`)
- **Node.js**: 20+ (for Vite client)
- **Entity Framework Core**: 9.0.9 (Sqlite provider, design, and tools packages)

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
From the `client-vite` directory:
```bash
npm install  # or pnpm install
npm run dev  # or pnpm dev
```
The Vite dev server will run on `http://localhost:5173`.

#### Client Configuration
Create a `.env.local` file in the `client-vite` directory (already gitignored):
```bash
# Use relative path to leverage Vite dev server proxy
VITE_API_BASE=/api
```

The Vite dev server is configured to proxy `/api/*` requests to the API server at `https://localhost:7226`. You can verify this by accessing `http://localhost:5173/api/health` in your browser or via curl.

### JWT configuration
- In production (or any deployed environment), provide a 256-bit or longer signing key via the `JWT__KEY` environment variable. The API refuses to start if the key is missing or too short.
- For local development you can optionally store a key with `dotnet user-secrets`:
  ```bash
  dotnet user-secrets set "Jwt:Key" "DevOnly_Minimum_32_Chars_Key_For_Local_Use_1234" --project ./api/api.csproj
  ```
  When no key is supplied in Development/Testing the API falls back to a deterministic development key and logs a warning on startup.

### Minimal developer seed data
- Ensure you are in the `api` directory, then run `dotnet run seed` to populate the SQLite database with three sample games and sets for UI testing. The command runs migrations first, skips if any cards already exist, and exits without starting the web server.

## Recent API additions
- `GET /api/health` – returns a simple health check status to verify API connectivity.
- `GET /api/cards/{id}/printings` – returns the available printings for a card, ordered by set and collector number.
- `GET /api/cards/{id}/sparkline?days=30` – returns aggregated value points for the card across its non-proxy printings.
- `GET /api/collection/value/history?days=90` – produces daily totals for the authenticated user's collection with proxies excluded.
- `GET /api/decks/{deckId}/value/history?days=90` – surfaces daily deck values across owned printings with proxies excluded.
- `POST /api/collection/items` – quick-add endpoint that increments the owned quantity for the authenticated user.
- `POST /api/wishlist/items` – quick-add endpoint that increments the desired quantity for the authenticated user.

## Admin operations
- `[RequireAdmin]` enforces administrator access based on the authenticated JWT principal and returns a `403` problem response otherwise.
- `/api/admin/users` exposes admin-only CRUD operations for users with optimistic UI support in the Vite client. The API prevents demoting or deleting the final administrator and returns a `409 Conflict` problem response when that safeguard triggers.
- `/api/admin/prices/ingest` ingests per-printing price points and safely upserts duplicates based on `(cardPrintingId, capturedAt)`.
