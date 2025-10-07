# Admin Import Guide

The admin import tools expose a unified workflow across both remote sources and file uploads.

## API endpoints

All routes require the `X-User-Id` header for an administrator.

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
