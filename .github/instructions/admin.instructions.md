---
applyTo: "api/Features/Admin/Sync/**,api/Infrastructure/Auth/Admin*.cs"
---

# Admin sync

- Importer and catalog sync endpoints (`/api/admin/sync/**`) are unauthenticated for local single-user operation; do not gate them behind `AdminSession` or user login.
- Mutations under `/api/admin/sync/**` are protected against cross-site browser requests by `AdminSyncAntiforgeryFilter`, which requires either an `X-Requested-With` header, a configured CORS `Origin`, or an `AdminToken` bearer credential.
- The SWCCG admin dry-run persists a sync log but does not advance the catalog checkpoint; do not assume other importers' dry-run logging behavior is the same.
- Two-step **"Sync Sets First"** sync panels (e.g., MTG, Pokémon, and future games using set-then-card sync): already-synced sets (`isSynced: true`) must always render checked and disabled (cannot be unchecked), era/category master checkboxes only toggle unsynced sets (`!isSynced`) and render checked & disabled when all sets in the group are synced, and **Sync Cards** (`Sync Cards (<new> new, <synced> synced)`) must send the union of all already-synced set codes and any newly selected unsynced set codes so existing sets are checked for new cards or updates.
