---
name: card-filters
description: End-to-end checklist and architecture guide for adding, modifying, or onboarding game-specific card filters across the .NET API, EF Core models, importers, and React frontend.
---

# Card Filters Skill

Use this skill whenever adding a new card filter, modifying an existing filter, or onboarding a new game's filters in Trading Card Game Tracker.

## Core Principles

1. **Header Name Search Is Shared & Untouched:** Card name search (`q`) lives in the header and must compose with all game-specific filters via `useCardFilters()`.
2. **No Empty Sets in `Set` Facets:** `CardFacetsController` queries `CardPrintings` (not `Sets` directly) so sets with zero synced cards are never listed.
3. **Game-Scoped UI Sections:** Each game has its own focused section component in `client-vite/src/features/printings/components/filters/` rendered inside `CardFiltersDrawer.tsx`. Never leak filters from one game into another game's drawer or active filter pills.
4. **Consistent Operator Semantics:**
   - Standard multiselects (`Set`, `Type`, `Subtype`, `Rarity`, `Stage`, `Special Type`, `Energy Type`, `Keyword` for MTG/SWU) use `OR` without showing an operator selector (omit `onMethodChange` on `MultiSelect`).
   - `Trait` (SWU) supports `OR`, `AND`, `EXCLUDE`.
   - `Aspect` (SWU) supports `OR`, `AND`, `EXCLUDE` with multiplicity (`Aggression,Aggression`).
   - `Colors` (MTG) supports `OR`, `AND` (`subset`: at least one selected, none unselected), and `EXACT` (`exact`: all selected, none unselected).
   - `Color Identity` (MTG) uses `EXCLUDE` semantics (exclude cards with any color in `CardColorIdentities` not in the selected set) with no operator UI.
   - Numeric text filters (`Cost` / `Mana Value`, `Power`, `Toughness`, `Loyalty`, `HP`, `Upgrade Power`, `Upgrade HP`, `Ability`) use `NumericFilterInput` and `QueryExtensions.ApplyNumericFilter` (supporting exact values and `>` / `<` operators).

## End-to-End Checklist

### 1. Database & Importer (if adding a new persisted stat or relation)
- Add nullable column(s) or join entity to `api/Models/Card.cs` and configure indexes in `api/Data/Configurations/`.
- Create an additive EF Core migration (`dotnet ef migrations add <Name> --project api/api.csproj --configuration Release`).
- Populate the new field/join rows in the relevant game importer (`api/Importing/...`).

### 2. Backend Query & Facets
- Add the query property to `api/Features/Cards/Dtos/ListPrintingsQuery.cs`.
- Apply the filter in `api/Features/Cards/PrintingsController.cs` (reusing `QueryExtensions.ApplyNumericFilter` or a dedicated `QueryExtensions` helper).
- Accept the query parameter in `api/Features/Cards/CardFacetsController.cs` (`GetFacets`) and apply it inside `ApplyStatFilters` so all facet dropdowns stay dynamically scoped.

### 3. Frontend State & API
- Add the field to `CardFilters`, `filterKeys`, `sanitizeFilters`, `parseFilters`, and `applyFiltersToParams` in `client-vite/src/features/cards/filters/useCardFilters.ts`.
- Add the field to `client-vite/src/features/cards/queryKeys.ts` (`OptionalExtraFilterFields`) if needed for cache keys.
- Forward the parameter in:
  - `client-vite/src/features/printings/api/printings.ts` (`PrintingQuery` & `fetchPrintings`)
  - `client-vite/src/features/printings/api/usePrintings.ts`
  - `client-vite/src/features/cards/api/list.ts` (`fetchCardFacets`)
  - `client-vite/src/features/printings/api/usePrintingFacets.ts`

### 4. Frontend UI & Views
- Render the control in the game's section component under `client-vite/src/features/printings/components/filters/`.
- Update `scopedFilters`, `activeFilters` pills, and `clearFilters` in `client-vite/src/features/printings/pages/CardsPage.tsx`.
- Update `clearFilters` / `hasActiveFilters` in `client-vite/src/features/decks/components/DeckConstructionModal.tsx` and `client-vite/src/features/decks/DeckBuilderPage.tsx`.

### 5. Verification
- Add/update xUnit integration tests in `api.Tests/Features/Cards/` (e.g. `GameCardFilterTests.cs`).
- Add/update Vitest unit/component tests for `useCardFilters`, the filter component, `CardFiltersDrawer`, and `CardsPage`.
- Run backend build/format/tests and frontend `lint:strict`, `typecheck`, `test -- --run`, `build`, and `build-storybook`.
