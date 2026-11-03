---
applyTo: "api/Importing/**,api/Features/Admin/**"
---

# SWU importers and sync

- Identity is `CardUid`; set/number/style fallback adopts only legacy printings without a UID.
- Aspect multiplicity lives in `CardAspect.Count`, never duplicate rows.
- Upserts are idempotent; `dryRun` writes a sync log but no catalog checkpoint.
- Sync endpoints require an AdminSession cookie + CSRF, or AdminToken. The client user selector is not an admin identity.
- Tests must cover run-twice-no-duplicates and `dryRun`.
