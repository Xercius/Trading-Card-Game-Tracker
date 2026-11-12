---
applyTo: "api/Importing/SwuDbImporter.cs,api/Importing/SwuSharedIdentity.cs,api/Importing/SwuCatalog.cs,api/Importing/SWUApiClient.cs"
---

# Star Wars: Unlimited importer

- `CardPrinting.CardUid` is the primary printing identity. When the incoming record has a `CardUid` but no UID matches, fallback to set, number, and treatment only when that key is unambiguous and the existing printing's `CardUid` and `DetailsJson.cardUid` are both absent/null; backfill the UID on a match. If the fallback key is ambiguous or the candidate printing already has a different UID, do not update any existing printing; create a new printing for the incoming record.
- If the incoming record has no `CardUid`, match by set, number, and treatment only when that key identifies one existing printing; if no unique match is found, create a new printing without a UID.
- Store repeated aspects in one `CardAspect` association with the combined multiplicity in `Count`.
- Resolve declared variant/reprint targets by card UID. If the reference has no card UID, resolve by source ID; do not fall back to source ID when a card UID is present but unmatched. Preserve existing variant/reprint links only when the incoming record contains no variant/reprint fields at all (neither UID nor source ID targets); treat these as legacy printings. Clear a declared target that cannot yet be resolved.
