---
applyTo: "api/Importing/Pokemon/**"
---

# Pokémon importer

- `Card.CardUid` is a normalized mechanical fingerprint, not the TCGdex card ID; cards with the same mechanics across sets/treatments share a card, while same-name cards with different mechanics remain distinct.
- `CardPrinting.CardUid` identifies a TCGdex card and its variant. Prefer the source `VariantId`; when absent and detailed variant data is present, combine the TCGdex card ID with the variant fields `Type`, `Treatment`, and `Stamp` in that order (trimmed, lower-cased, nulls as empty string, joined with `|`), never from collector number alone. If neither `VariantId` nor detailed variant data is present, create a single printing with variant identity `default` combined with the TCGdex card ID.
- Use the TCGdex set ID as `Set.Code` and its `LocalId` as the collector number.
- When detailed variants are present, omit jumbo variants if any non-jumbo variant is available.
