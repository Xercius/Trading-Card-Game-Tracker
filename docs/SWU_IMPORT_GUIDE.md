# Star Wars Unlimited import guide

This guide describes the current Star Wars Unlimited (SWU) import paths and their operational guarantees. The implementation, not older roadmap wording, determines what is available today.

Historical roadmap context:

- #541 — add dedicated `SWUSet`, `SWUCard`, `SWUCardPrinting`, and `SyncLog` models
- #542 — add DbContext configuration for those SWU entities
- #544 — introduce a dedicated `SWUApiClient`
- #546 — implement insert/update persistence for SWU data
- #553 — manually validate real sync behavior and timestamp tracking
- #557 — document limitations, gotchas, and workarounds after implementation/testing

Use `/home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/docs/SWUAPI_DOCUMENTATION.txt` as the low-level API contract reference. Use this guide for repository-specific implementation details.

## Who

- **Admin users** run SWU imports through `/api/admin/import/*`.
- **`AdminImportController`** runs dry-run/apply flows and records checkpoints only for unlimited, error-free remote apply runs.
- **`AdminSyncController`** runs incremental imports for known SWU sets, records results, and supports full reconciliation.
- **`SwuDbImporter`** is the current SWU importer implementation.
- **`ImporterRegistry`** exposes `swu` as a normal source alongside the other games.
- **`SWUApiClient`** already handles fetching, pagination, filtering, and rate-limit retries for both persistence paths.
- **`CardSyncService`** maintains the separate dedicated SWU tables. It is not the persistence path used by the admin sync button.

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

### Dedicated persistence and compatibility

Dedicated `SwuSet`, `SwuCard`, `SwuCardPrinting`, and `SyncLog` entities, migrations, DbContext configuration, `SWUApiClient`, and `CardSyncService` already exist.

The shared `Card`/`CardPrinting` tables remain authoritative for the application's card browsing, collections, decks, and wishlists. Those features reference shared printing IDs; pointing the admin button at dedicated tables would not update the cards they use.

Treat dedicated SWU persistence as a separate integration path until an explicit, ownership-preserving bridge or migration is designed. Do not delete/recreate shared printings to switch storage paths.

Dedicated synchronization stores its checkpoint under `swu-dedicated`, separate from the admin/shared importer's `swu` history. Existing ambiguous `swu` history is not trusted for dedicated-table coverage, so the first dedicated run after this change performs a full sync. Automatic dedicated deltas overlap by five minutes; an explicit lower bound is used as supplied.

The additive `RetainSwuUpstreamBaseReferences` migration retains unresolved upstream relationship targets in nullable `SwuCard.BaseStrapiId`. Apply migrations through the normal deployment workflow. No existing ownership tables or applied migrations are rewritten.

## When

- Use **dry-run first** whenever you are changing mapping or testing new API behavior.
- Use **apply** only after the preview looks correct.
- Use **remote import** when syncing directly from the official SWU API.
- Use **file import** when replaying captured Strapi payloads or building deterministic tests.
- **Run sync** automatically uses the saved shared checkpoint minus one minute; no manual date is required. A set without history receives a full import.
- **Full reconciliation** ignores checkpoints and imports every record in each known set. Use it periodically, after suspected upstream changes, and once when upgrading from older releases whose checkpoints could advance after limited or partially failed imports.
- Record checkpoints **only for complete, error-free remote apply runs**. File imports, dry-runs, limited runs, and failed runs cannot establish full-set coverage.
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
- The shared importer keeps existing collection, deck, wishlist, and pricing references intact.
- Timestamps (`createdAt`, `updatedAt`, `publishedAt`) make incremental sync possible, so the repository already tracks per-importer/per-set sync times.
- Separate storage paths must not share synchronization checkpoints: fetching into dedicated tables does not update shared inventory.

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
6. Unlimited, error-free remote apply runs record `ImportSyncHistory(ImporterKey, SetCode, LastSyncedAt)` after commit. Remote apply without an explicit limit is not capped to the preview limit.
7. The sync panel calls `POST /api/admin/sync/star-wars-unlimited`; `?forceFullSync=true` requests reconciliation. Existing routes and response fields are retained. A partially failed run returns `status: "Failed"` even when some sets succeeded; only successful sets advance their checkpoints.

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
  - `id:asc` as a unique tie-breaker
- adds `filters[updatedAt][$gt]=<ISO-8601 UTC>` when `UpdatedSince` is supplied
- rejects malformed, truncated, or inconsistent pagination and repeated upstream IDs instead of treating an incomplete response as a successful sync

### 3. File import behavior

Current `SwuDbImporter.ImportFromFileAsync` accepts:

- a full Strapi response object
- or a bare array of records for backwards compatibility

This is the preferred path for unit tests because it avoids live API dependencies.

### 4. Mapping rules

| SWU field | Current repository behavior |
|---|---|
| `title` + `subtitle` | Combined display name; upstream printing identity takes precedence when updating an existing card, so renaming does not replace its owned printing. |
| `type.data.attributes.name` | Stored as `Card.CardType`. |
| `text` | Stored as `Card.Description`. |
| `expansion.data.attributes.code` | Stored as uppercase `CardPrinting.Set`; falls back to `UNK` when missing. |
| `serialCode` | Preferred printing identity and stored as `CardPrinting.Number` when present. |
| `cardNumber` | Used as fallback printing number; if missing, record `id` is used. |
| `rarity` | Stored as `CardPrinting.Rarity`; falls back to `Unknown`. |
| `variantTypes[*].foil` | Any `true` value marks the printing style as `Foil`; otherwise `Standard`. |
| `artFront` / `artBack` | Image preference order is art front original URL, card format URL, thumbnail URL, then art back URL. |
| `variantOf` | Deferred relationship resolution uses upstream identifiers rather than titles, preventing subtitle mismatches and import-order dependence. Self-links are excluded; unresolved metadata is retained for subsequent imports. |
| `createdAt` / `updatedAt` | Preserved in `CardPrinting.DetailsJson` for sync/debugging. |
| traits / keywords / aspects / arena / cost / power / health / artist / cardUid / reprint data | Preserved in `DetailsJson`. |

### 5. Operational gotchas

- **English only:** non-`en` records are skipped intentionally.
- **Set list gap:** the importer requires a set code for remote imports, but SWU does not currently have static set options defined in `AdminImportController`; callers must already know the code.
- **Two persistence paths:** dedicated SWU tables exist, but the admin import/sync workflows update shared tables for compatibility.
- **Two tracking purposes:** `SyncLog` audits sync attempts; `ImportSyncHistory` stores reusable coverage checkpoints. A completed attempt is not necessarily a successful synchronization.
- **Artwork:** image URLs are stored; image files are not downloaded.
- **Pagination:** pages are fetched before shared database writes begin. A later-page exception cannot advance the checkpoint. Upstream page-number pagination is not a snapshot; overlap and periodic reconciliation remain necessary.
- **Remote sync state is per importer + set:** each SWU set can have its own `LastSyncedAt`.
- **Checkpoint timing:** save the run-start time only after all pages and applicable records succeed. A failed checkpoint write causes safe re-fetching, not skipped records. Per-row API timestamps are metadata, not proof of complete set coverage.
- **Dedicated transaction:** SWU rows, their checkpoint, and the completed audit log commit together. Limited dedicated runs are logged as `Partial`; rejected records produce `Failed`, and neither advances coverage.
- **Preview scope:** the preview is capped, while an unlimited remote apply processes the whole set. It is not a guaranteed immutable snapshot of a changing upstream API.

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
2. Check which persistence path the caller uses before modifying identity or synchronization.
3. Use dry-run before apply.
4. Preserve incremental sync behavior based on `updatedAt`.
5. Keep `api.Tests/Importing/SwuDbImporterTests.cs` in sync with mapping or query changes.
6. Keep checkpoints isolated between shared and dedicated persistence; do not use a dedicated-table sync to advance shared inventory coverage.

## Validation

Use the existing SQLite importer/sync-service tests and admin endpoint tests for repeat imports in fresh contexts, duplicate records, renames with ownership/deck references, relationship ordering, rejected records, limits, and failed later pages. Client sync-panel tests cover incremental/full requests and partial-failure feedback.

A read-only, anonymous one-card connectivity check was attempted on 2026-10-20 using the existing script, but this environment could not resolve `admin.starwarsunlimited.com`. Live upstream compatibility remains unverified here. Re-run the script locally, then preview/apply a small captured response in a disposable database before reconciling a real collection.
