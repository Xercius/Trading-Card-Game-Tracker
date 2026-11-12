---
applyTo: "api/Features/Admin/Sync/**,api/Infrastructure/Auth/Admin*.cs"
---

# Admin sync

- Admin sync operations require an `Admin` role authenticated through `AdminToken` or `AdminSession`; the client-selected app user is not an administrator identity.
- Browser requests authenticated by the `AdminSession` cookie must include the antiforgery request token on mutations. Bearer-authenticated requests are exempt from antiforgery validation.
- The SWCCG admin dry-run persists a sync log but does not advance the catalog checkpoint; do not assume other importers' dry-run logging behavior is the same.
