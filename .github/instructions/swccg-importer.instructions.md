---
applyTo: "api/Importing/Swccg/**"
---

# Star Wars CCG importer

- The registered `SwccgJsonImporter` uses `GempId` as the card and printing source identity, falling back to the source record ID when it is absent. For a `GempId` in `<set>_<number>` form, derive the collector number from the suffix. If `GempId` is absent or not in `<set>_<number>` form, use the source record's explicit collector number field if present; otherwise leave the collector number null rather than inventing one.
- Resolve source set references through set ID, abbreviation, name, or GEMP name. When metadata resolves the set, use its abbreviation as the shared set code when available, otherwise its source ID.
- Preserve SWCCG variants as distinct treatments: `(V)` denotes `Virtual`, `(AI)` denotes `Alternate Image`, and a rarity value that equals `Foil` or ends with ` Foil` (case-insensitive) denotes the `Foil` treatment; other rarity values do not imply a treatment. A printing may carry multiple treatments (e.g. `Virtual` and `Foil`); record all that apply rather than selecting one.
