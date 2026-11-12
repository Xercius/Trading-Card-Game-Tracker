---
applyTo: "api/Importing/**"
---

# Importers

- Implement source adapters through `ISourceImporter` and register each with a unique key in `ImporterRegistry`. Keep provider parsing, identity, and normalization in game-specific importers/catalogs rather than shared code.
- Replaying the same source data must be idempotent: resolve existing cards, printings, sets, and associations using that importer's identity rules instead of creating duplicates.
