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

## Recent API additions
- `GET /api/cards/{id}/printings` – returns the available printings for a card, ordered by set and collector number.
- `GET /api/prices/{printingId}/history?days=30` – provides a 30-day sparkline-friendly series of daily closing prices for a card printing.
- `POST /api/collection/items` – quick-add endpoint that increments the owned quantity for the authenticated user.
- `POST /api/wishlist/items` – quick-add endpoint that increments the desired quantity for the authenticated user.
