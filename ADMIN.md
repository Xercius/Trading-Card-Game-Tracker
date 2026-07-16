# Admin Import Guide

The admin import tools expose a unified workflow across both remote sources and file uploads.

## API endpoints

All routes require an administrator authenticated via JWT bearer token.

- `GET /api/admin/import/options` — returns the available importers, supported games, and known set codes. Use this to populate selection lists in the UI.
- `POST /api/admin/import/dry-run` — accepts either a JSON payload (`{ "source": "dummy", "set": "ALP" }`) or `multipart/form-data` with `source` and a `.csv`/`.json` upload. No database changes are persisted; the response includes a summary with new/update counts and preview rows.
- `POST /api/admin/import/apply` — mirrors the dry-run payloads but commits the changes.
- `POST /api/admin/prices/ingest` — ingests daily price snapshots. Each array item should include `cardPrintingId`, `capturedAt` (`YYYY-MM-DD`), and a decimal `price`. The endpoint upserts on `(cardPrintingId, capturedAt)` and ignores invalid printings.

All dry-run/apply requests honor the importer-specific options (set codes, limit, etc.). Errors are returned as `ProblemDetails` with a `400` status.

## File uploads

- Supported file types: `.csv` and `.json`.
- Maximum upload size: **10 MB** (enforced in the client and server validators).
- CSV headers must include `name`, `set`, and `number` columns.
- JSON payloads must be an array of objects with `name`, `set`/`set_code`, and `number`/`collector_number` fields.

The UI preview is capped to **250 rows** to keep rendering responsive.

## Example workflow

1. Open **Admin → Import** in the navigation bar.
2. Choose a source and either a set code (remote mode) or drag a `.csv`/`.json` file onto the dropzone.
3. Click **Dry-run** to fetch a preview. Review the summary pills and status table for duplicates, errors, or other messages.
4. Click **Apply** to commit changes. A success toast confirms the counts and resets the form.

The UI automatically clears stale previews after an apply operation so the next import starts with a clean slate.

## Star Wars Unlimited set endpoint

After importing SWU cards, the available expansion codes (sets) are accessible through the standard card facets API.

### Retrieve all SWU sets

```
GET /api/cards/facets/sets?game=Star+Wars+Unlimited
```

**Response schema:**

```json
{
  "game": "Star Wars Unlimited",
  "sets": ["SHD", "SOR", "TWI"]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `game` | `string` | The game filter applied. Present when a `game` query parameter is supplied. |
| `sets` | `string[]` | Distinct expansion codes stored during import, sorted alphabetically. Each code matches the `attributes.expansion.data.attributes.code` field from the SWU Strapi API, normalised to uppercase (e.g. `"SOR"`, `"SHD"`, `"TWI"`). |

The sets list reflects only expansions that have at least one imported card printing.

### Retrieve SWU sets with counts

The combined facets endpoint returns set counts alongside game and rarity breakdowns:

```
GET /api/cards/facets?game=Star+Wars+Unlimited
```

**Response schema (sets portion):**

```json
{
  "sets": [
    { "value": "SHD", "count": 252 },
    { "value": "SOR", "count": 252 },
    { "value": "TWI", "count": 252 }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `sets[].value` | `string` | Expansion code (e.g. `"SOR"`). |
| `sets[].count` | `integer` | Number of card printings in that expansion. |

### Known SWU expansion codes

| Code | Name |
|------|------|
| `SOR` | Spark of Rebellion |
| `SHD` | Shadows of the Galaxy |
| `TWI` | Twilight of the Republic |
| `JTL` | Jump to Lightspeed |

Expansion codes are sourced from the SWU API during import and stored verbatim (uppercase) in the `CardPrinting.Set` column. New expansions appear automatically after a fresh import.

## Star Wars Unlimited API connectivity check

Use the smoke-test script at `/api/scripts/test-swu-api-connectivity.sh` to verify that the SWU API is reachable, authentication works (when credentials are provided), and the response shape matches the Strapi card-list format expected by the importer.

```bash
# Anonymous request (documents whether the endpoint is publicly readable)
./api/scripts/test-swu-api-connectivity.sh

# Auth flow (optional, if SWU credentials are required in your environment)
SWU_API_IDENTIFIER="your-login" \
SWU_API_PASSWORD="your-password" \
./api/scripts/test-swu-api-connectivity.sh
```

The script validates:
- `data` is a non-empty array
- each record includes `id` and `attributes`
- `meta.pagination` includes `page`, `pageSize`, `pageCount`, and `total`

The expansion code for each card is embedded in the card-list response under `attributes.expansion.data.attributes.code`. A dedicated expansion-list endpoint is not required; all set metadata needed for filtering is derived from the card data during import.
