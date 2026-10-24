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
- **`AdminSyncController`** bootstraps the full catalog, runs global incremental imports across all expansions, records results, and supports full reconciliation.
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
- expansion codes and names are upserted into `SwuSet`
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
- **Run sync** fetches the full catalog when no global checkpoint exists. Later runs fetch all records updated after that checkpoint minus one minute, without expansion filters, so new expansions are discovered automatically.
- **Full reconciliation** ignores checkpoints and imports the unfiltered catalog, including previously unknown expansions. Use it periodically, after suspected upstream changes, and once when upgrading from older releases whose checkpoints could advance after limited or partially failed imports.
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

### Disposable database for local live-sync testing

The API and EF migration tooling read `ConnectionStrings:DefaultConnection` from
the normal ASP.NET Core configuration providers (JSON settings, user secrets,
environment variables, or command-line arguments). With no override, both retain
`Data Source=app.db`. Relative SQLite paths are relative to the process working
directory, not the settings file; use an absolute path for disposable testing.

Stop any normally running API first so the UI cannot accidentally sync against
it. In a dedicated Bash terminal, start a subshell with a fresh temporary directory:

```bash
(
  cd /home/runner/work/Trading-Card-Game-Tracker/Trading-Card-Game-Tracker/api
  disposable_dir="$(mktemp -d)"
  export ConnectionStrings__DefaultConnection="Data Source=$disposable_dir/swu-live.db"
  printf 'Disposable database: %s/swu-live.db\n' "$disposable_dir"
  printf 'Connection override: %s\n' "$ConnectionStrings__DefaultConnection"

  dotnet run --project "$PWD/api.csproj" --launch-profile "TradingCardApi (HTTPS)"
)
```

Keep this terminal open during testing. Normal startup creates the selected file,
applies all pending EF migrations, and runs the existing seed routine before
serving requests. Do **not** use the `Testing` environment for this live workflow:
it skips startup migrations and seeding. The normal database and its
`ImportSyncHistory` checkpoints are not copied, reset, or modified. A fresh
database has no sync checkpoints, so its first live sync requests the full catalog.

If the EF CLI is installed, you can inspect the selected connection and initialize
the schema explicitly **inside the subshell, after exporting the override and
before starting the API**:

```bash
dotnet ef dbcontext info --project "$PWD/api.csproj"
dotnet ef database update --project "$PWD/api.csproj"
```

Confirm that `Data source` in `dbcontext info` is the printed absolute disposable
path before applying migrations or syncing. EF tooling uses the same configuration
as the API; startup also applies any remaining migrations. With the SQLite CLI,
you can verify the created file and migration history in another terminal
(substitute the printed path):

```bash
sqlite3 -readonly /tmp/your-disposable-directory/swu-live.db \
  'PRAGMA database_list; SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;'
```

For browser sync, provision an administrator using the [admin authentication
instructions](../ADMIN.md#sync-authentication) before starting the server, in the
same subshell with the override still exported. Alternatively, provide
`Authentication__AdminApiToken` only through the server environment for trusted
script clients. Never commit admin tokens or passwords to settings files or
browser configuration. Verify the disposable connection first, then dry-run
before applying imports or using **Run sync**.

To restore normal operation, stop the test API with Ctrl+C and let the subshell
exit. Its exported override then disappears automatically; restart the API from
your usual working directory/terminal with your original configuration. If you
set the override outside a subshell, run `unset ConnectionStrings__DefaultConnection`;
also remove any disposable override you added to JSON, user secrets, or launch
arguments. With no override, the original working-directory `app.db` is used again.
Do not delete or reset that database or its checkpoints. The temporary directory
may be retained for inspection; switching back requires no database deletion.

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
7. The sync panel calls `POST /api/admin/sync/star-wars-unlimited`. A missing global checkpoint requests the full catalog; subsequent runs use an `updatedAt` filter with a one-minute overlap. Neither run filters by expansion. `?forceFullSync=true` ignores the checkpoint. Only complete, error-free imports advance `ImportSyncHistory(ImporterKey: "swu-catalog", SetCode: "")`, atomically with the successful audit log. Existing `swu` per-set history is neither reused nor modified. Failed runs retain the prior checkpoint, even if some records were upserted; retries safely replay those records. Status reports the last successful global checkpoint and latest global outcome separately.

### 2. Remote import behavior

Current `SwuDbImporter.ImportFromRemoteAsync`:

- imports the full catalog when `SetCode` is omitted or blank, without an expansion lookup or filter
- retains single-set imports when `SetCode` is explicitly supplied:
  - normalizes the set code to uppercase
  - resolves the SWU expansion's numeric Strapi ID first when possible
  - falls back to `filters[expansion][code][$eq]` if ID resolution fails
- derives each printing's set from its record's expansion metadata, including previously unknown expansions
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
| `expansion.data.attributes.code` / `name` | Stored as uppercase `CardPrinting.Set` and upserted into `SwuSet`. Global remote imports reject missing codes/names; legacy file imports retain the `UNK` fallback. |
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
- **Set discovery:** no pre-populated or hard-coded set list is required for full-catalog imports. Explicit single-set imports still require the caller to supply a code.
- **Two persistence paths:** dedicated SWU tables exist, but the admin import/sync workflows update shared tables for compatibility.
- **Two tracking purposes:** `SyncLog` audits sync attempts; `ImportSyncHistory` stores reusable coverage checkpoints. A completed attempt is not necessarily a successful synchronization.
- **Artwork:** image URLs are stored; image files are not downloaded.
- **Pagination:** pages are fetched before shared database writes begin. A later-page exception cannot advance the checkpoint. Upstream page-number pagination is not a snapshot; overlap and periodic reconciliation remain necessary.
- **Checkpoint isolation:** the sync button uses only `swu-catalog` with an empty set code. Legacy `swu` per-set history (including zero-card runs) and `swu-dedicated` history do not establish global coverage.
- **Checkpoint timing:** save the run-start time only after all pages and applicable records succeed. A failed checkpoint write causes safe re-fetching, not skipped records. Per-row API timestamps are metadata, not proof of complete set coverage.
- **Dedicated transaction:** SWU rows, their checkpoint, and the completed audit log commit together. Limited dedicated runs are logged as `Partial`; rejected records produce `Failed`, and neither advances coverage.
- **Preview scope:** the preview is capped, while an unlimited remote apply processes the whole requested feed (one set or the full catalog). It is not a guaranteed immutable snapshot of a changing upstream API.

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

Omit `set` to import the full catalog into shared tables:

```json
POST /api/admin/import/apply
{
  "source": "swu"
}
```

Supply `set` to retain the single-expansion behavior:

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
