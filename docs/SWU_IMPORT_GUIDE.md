# Star Wars Unlimited import guide

This guide explains the repository's current Star Wars Unlimited (SWU) import implementation and the roadmap direction that developers should treat as authoritative when the two differ.

If this file conflicts with open SWU roadmap issues, prefer the issues:

- #541 — add dedicated `SWUSet`, `SWUCard`, `SWUCardPrinting`, and `SyncLog` models
- #542 — add DbContext configuration for those SWU entities
- #544 — introduce a dedicated `SWUApiClient`
- #546 — implement insert/update persistence for SWU data
- #553 — manually validate real sync behavior and timestamp tracking
- #557 — document limitations, gotchas, and workarounds after implementation/testing

Use `/home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/docs/SWUAPI_DOCUMENTATION.txt` as the low-level API contract reference. Use this guide for repository-specific implementation details.

## Who

- **Admin users** run SWU imports through `/api/admin/import/*`.
- **`AdminImportController`** parses requests, resolves the importer, runs dry-run/apply flows, and records remote sync timestamps.
- **`SwuDbImporter`** is the current SWU importer implementation.
- **`ImporterRegistry`** exposes `swu` as a normal source alongside the other games.
- **Future owners of SWU-specific behavior** are expected to move logic into the roadmap items above, especially a dedicated `SWUApiClient` and SWU-specific data model.

## What

### Current implementation

Today SWU is implemented as a generic importer:

- importer key: `swu`
- display name: `Star Wars: Unlimited (Official API)`
- supported game: `Star Wars Unlimited`
- remote source: `https://admin.starwarsunlimited.com/api/card-list`
- accepted file formats:
  - full Strapi response objects: `{ "data": [...], "meta": {...} }`
  - bare JSON arrays of Strapi records

The importer writes into the shared tables instead of SWU-specific tables:

- logical card data goes into `Card`
- printing data goes into `CardPrinting`
- SWU-only metadata is serialized into `Card.DetailsJson` and `CardPrinting.DetailsJson`
- successful remote apply runs store last-sync state in `ImportSyncHistory`

### Planned implementation

The roadmap says SWU should eventually stop being "just another generic importer" and grow into a dedicated import stack:

- SWU-specific EF models (`SWUSet`, `SWUCard`, `SWUCardPrinting`, `SyncLog`) — #541
- explicit DbContext configuration for those entities — #542
- a dedicated `SWUApiClient` that owns auth, rate limiting, pagination, and error handling — #544
- explicit insert/update persistence for cards, sets, and printings — #546
- real sync validation against the live API, including timestamp tracking — #553
- final limitations/gotchas documentation once the design settles — #557

Treat the current importer as an interim implementation that already proves field mapping and delta-query behavior.

## When

- Use **dry-run first** whenever you are changing mapping or testing new API behavior.
- Use **apply** only after the preview looks correct.
- Use **remote import** when syncing directly from the official SWU API.
- Use **file import** when replaying captured Strapi payloads or building deterministic tests.
- Pass **`UpdatedSince`** for incremental remote imports after a previous successful apply run.
- Record sync timestamps **only for remote apply runs**. File imports do not represent live API sync state.
- Re-run `/home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/api/scripts/test-swu-api-connectivity.sh` whenever the SWU API changes or starts failing unexpectedly.

## Where

- Current importer: `/home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/api/Importing/SwuDbImporter.cs`
- Shared import options/models: `/home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/api/Importing/ImportModels.cs`
- Admin entrypoint: `/home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/api/Features/Admin/Import/AdminImportController.cs`
- Import sync history model: `/home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/api/Models/ImportSyncHistory.cs`
- DI registration: `/home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/api/Infrastructure/Startup/ServiceCollectionExtensions.cs`
- API behavior notes: `/home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/docs/SWUAPI_DOCUMENTATION.txt`
- Connectivity smoke test: `/home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/api/scripts/test-swu-api-connectivity.sh`
- Importer test coverage: `/home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/api.Tests/Importing/SwuDbImporterTests.cs`

## Why

- The upstream SWU API is an internal Strapi endpoint and may change without notice.
- The current generic importer let the project ship SWU support quickly without introducing new SWU-only tables.
- Timestamps (`createdAt`, `updatedAt`, `publishedAt`) make incremental sync possible, so the repository already tracks per-importer/per-set sync times.
- The roadmap still wants a more explicit SWU architecture because generic `Card`/`CardPrinting` storage and `DetailsJson` are not the final design.

## How

### 1. Request flow

1. An admin calls `POST /api/admin/import/dry-run` or `POST /api/admin/import/apply`.
2. `AdminImportController` resolves `source: "swu"` to `SwuDbImporter`.
3. The controller builds `ImportOptions` from:
   - `set`
   - `limit`
   - `updatedSince`
   - dry-run/apply mode
4. If a file is present, the controller routes to `ImportFromFileAsync`.
5. Otherwise it routes to `ImportFromRemoteAsync`.
6. Remote apply runs record `ImportSyncHistory(ImporterKey, SetCode, LastSyncedAt)` after success.

### 2. Remote import behavior

Current `SwuDbImporter.ImportFromRemoteAsync`:

- requires `SetCode`
- normalizes the set code to uppercase
- resolves the SWU expansion's numeric Strapi ID first when possible
- falls back to `filters[expansion][code][$eq]` if ID resolution fails
- always requests `locale=en`
- pages through `meta.pagination.pageCount`
- sorts by:
  - `updatedAt:asc`
  - `cardNumber:asc`
- adds `filters[updatedAt][$gt]=<ISO-8601 UTC>` when `UpdatedSince` is supplied

### 3. File import behavior

Current `SwuDbImporter.ImportFromFileAsync` accepts:

- a full Strapi response object
- or a bare array of records for backwards compatibility

This is the preferred path for unit tests because it avoids live API dependencies.

### 4. Mapping rules

| SWU field | Current repository behavior |
|---|---|
| `title` + `subtitle` | Card identity is the combined display name. Subtitle is appended with `—` when present so same-title/different-subtitle cards do not collide. |
| `type.data.attributes.name` | Stored as `Card.CardType`. |
| `text` | Stored as `Card.Description`. |
| `expansion.data.attributes.code` | Stored as uppercase `CardPrinting.Set`; falls back to `UNK` when missing. |
| `serialCode` | Preferred printing identity and stored as `CardPrinting.Number` when present. |
| `cardNumber` | Used as fallback printing number; if missing, record `id` is used. |
| `rarity` | Stored as `CardPrinting.Rarity`; falls back to `Unknown`. |
| `variantTypes[*].foil` | Any `true` value marks the printing style as `Foil`; otherwise `Standard`. |
| `artFront` / `artBack` | Image preference order is art front original URL, card format URL, thumbnail URL, then art back URL. |
| `variantOf` | Used to populate `BaseCardId` when the base card is already known. |
| `createdAt` / `updatedAt` | Preserved in `CardPrinting.DetailsJson` for sync/debugging. |
| traits / keywords / aspects / arena / cost / power / health / artist / cardUid / reprint data | Preserved in `DetailsJson`. |

### 5. Operational gotchas

- **English only:** non-`en` records are skipped intentionally.
- **Set list gap:** the importer requires a set code for remote imports, but SWU does not currently have static set options defined in `AdminImportController`; callers must already know the code.
- **Roadmap mismatch:** open issues plan dedicated SWU entities and a dedicated API client, but the live code still stores SWU data in generic tables plus JSON blobs.
- **Sync tracking mismatch:** the roadmap mentions `SyncLog`, while the live code currently uses `ImportSyncHistory`.
- **Base card linking is opportunistic:** `BaseCardId` is only set when the referenced base card has already been imported.
- **Remote sync state is per importer + set:** each SWU set can have its own `LastSyncedAt`.

### 6. API and script examples

#### Dry-run remote import

```json
POST /api/admin/import/dry-run
{
  "source": "swu",
  "set": "SOR",
  "updatedSince": "2026-07-01T00:00:00Z"
}
```

#### Apply remote import

```json
POST /api/admin/import/apply
{
  "source": "swu",
  "set": "SOR"
}
```

#### Connectivity check

```bash
cd /home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker
./api/scripts/test-swu-api-connectivity.sh
```

Optional auth can be supplied with `SWU_API_IDENTIFIER` + `SWU_API_PASSWORD` or `SWU_API_TOKEN`, but the current live importer itself uses unauthenticated GETs.

## Developer checklist

When working on SWU imports:

1. Read this file and `docs/SWUAPI_DOCUMENTATION.txt`.
2. Check the open SWU roadmap issues before assuming the current code is the target design.
3. Use dry-run before apply.
4. Preserve incremental sync behavior based on `updatedAt`.
5. Keep `api.Tests/Importing/SwuDbImporterTests.cs` in sync with mapping or query changes.
6. If you move toward the roadmap design, document whether `ImportSyncHistory` remains temporary or is replaced by the planned `SyncLog`.
