---
applyTo: "api/Features/Admin/Sync/**,api/Infrastructure/Auth/Admin*.cs"
---

# Admin sync

- Importer and catalog sync endpoints (`/api/admin/sync/**`) are unauthenticated for local single-user operation; do not gate them behind `AdminSession` or user login.
- Mutations under `/api/admin/sync/**` are protected against cross-site browser requests by `AdminSyncAntiforgeryFilter`, which requires either an `X-Requested-With` header, a configured CORS `Origin`, or an `AdminToken` bearer credential.
- The SWCCG admin dry-run persists a sync log but does not advance the catalog checkpoint; do not assume other importers' dry-run logging behavior is the same.
