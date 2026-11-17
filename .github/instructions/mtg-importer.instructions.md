---
applyTo: "api/Importing/Mtg/**"
---

# Magic: The Gathering (Scryfall) importer

- Canonical game name is `MtgCatalog.GameName` (`"Magic The Gathering"`). Register the importer under key `"mtg"` via `ISourceImporter`.
- `Card.CardUid` is the Scryfall `oracle_id` (falling back to the first non-blank face `oracle_id`, then a deterministic SHA-256 hash `mtg-<hex>` of `Name|TypeLine|OracleText` via `MtgCatalog.ComputeCanonicalCardUid`).
- Persist logical MTG attributes (`oracleId`, `layout`, `manaCost`, `cmc`, `typeLine`, `colors`, `colorIdentity`, `commanderLegality`, power/toughness/loyalty/defense, and back-face fields) in `Card.ExtraCardDataJson` via `MtgCatalog.BuildExtraCardDataJson`.
- `Set.Code` is the upper-cased Scryfall set code; skip digital-only sets (`s.Digital == true`) during `SyncSetsAsync`.
- Create one `CardPrinting` per distinct physical finish in `ScryfallCard.Finishes` (defaulting to `"nonfoil"`), mapping frame/finish attributes to canonical `Treatment` names via `MtgCatalog.ResolveTreatmentName`.
- Sanitize all set icon and card image URLs through `ImportHttpGuard.SanitizeAllowedImageUrl` using the `AllowedImageHosts` allow-list (`cards.scryfall.io`, `c1.scryfall.com`, `c2.scryfall.com`, `svgs.scryfall.io`).
