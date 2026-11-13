# Cards Filter State Architecture

## Overview

The Cards page uses a centralized filter state management system that ensures:

- Single source of truth for all filter values
- URL synchronization (filters persist in URL)
- Proper debouncing for text search
- Stable query keys for TanStack Query

## Key Components

### 1. `useCardFilters` Hook

Location: `client-vite/src/features/cards/filters/useCardFilters.ts`

This is the **single source of truth** for all filter state. It manages:

- `q`: text search query (string)
- `games`, `sets`, `rarities`, `types`, `arenas`: multi-select filter arrays (`string[]`)
- `aspects`, `aspectMethod`, `traits`, `traitMethod`, `keywords`, `keywordMethod`: SWU multi-select filters with `"or" | "and" | "exclude"` match method
- SWCCG & Pokémon-specific filters: `side`, `personas`, `subTypes`, `destinies`, `deploys`, `forfeits`, `powers`, `abilities`, `lightSideIcons`, `darkSideIcons`, `hps`, `stages`
- `page`, `pageSize`, `sort`: pagination and sort state
- `clearAll()`: resets all active filters

**Features:**

- Automatically syncs with URL search parameters
- Uses `replace: true` to avoid polluting browser history
- Provides stable query keys via `toQueryKey()`
- Sanitizes and validates all inputs

### 2. Query Keys

Location: `client-vite/src/features/cards/queryKeys.ts`

Centralized query key factory that ensures:

- Predictable, stable keys for caching
- No unnecessary refetches
- Proper invalidation patterns

### 3. CardsPage Integration

Location: `client-vite/src/features/printings/pages/CardsPage.tsx`

The main Cards listing page uses `useCardFilters` for all filter state:

```tsx
const { filters, setFilters } = useCardFilters();
const { data, isLoading, isError, error } = usePrintings(filters);
```

**Debounced Search:**

- User input is stored in local state
- Debounced by 250ms before updating filters
- Preserves input value during typing
- Handles browser back/forward navigation

### 4. API Integration

Location: `client-vite/src/features/printings/api/usePrintings.ts`

Converts `CardFilters` (and optional `availableAspects`) to `PrintingQuery` format, setting `uniqueCards: true`, capping `pageSize` at `500`, and forwarding all core, SWU, SWCCG, and Pokémon filter fields (`q`, `game`, `set`, `rarity`, `type`, `arena`, `aspect`, `aspectMethod`, `trait`, `traitMethod`, `keyword`, `keywordMethod`, `side`, `persona`, `subType`, `destiny`, `deploy`, `forfeit`, `power`, `ability`, `lightSideIcon`, `darkSideIcon`, `hp`, `stage`, `sort`, `page`, `pageSize`).

## Data Flow

```
User Action → Local State (immediate)
           ↓
           Debounce (250ms for text search)
           ↓
           useCardFilters → URL Params
           ↓
           usePrintings → API Query
           ↓
           TanStack Query (with stable keys)
```

## Usage Examples

### Setting a single filter

```tsx
setFilters((prev) => ({ ...prev, games: ["Star Wars: Unlimited"], page: 1 }));
```

### Updating search text (debounced in component)

```tsx
const [searchInput, setSearchInput] = useState(filters.q);
const debouncedSearch = useDebounce(searchInput, 250);

useEffect(() => {
  setFilters((prev) => ({ ...prev, q: debouncedSearch, page: 1 }));
}, [debouncedSearch, setFilters]);
```

### Clearing filters

```tsx
setFilters((prev) => ({
  ...prev,
  games: [],
  sets: [],
  rarities: [],
  page: 1,
}));
```

## Benefits

1. **No State Duplication**: Single source of truth prevents drift
2. **URL Persistence**: Filters survive page reload and can be shared
3. **Navigation Support**: Back/forward buttons work correctly
4. **Predictable Caching**: Stable query keys prevent unnecessary refetches
5. **Type Safety**: Full TypeScript support throughout
6. **Testability**: Clean separation makes testing easier

## Migration Notes

Previously, CardsPage used `usePrintingSearch` which maintained separate state.
This has been replaced with `useCardFilters` for consistency with other
components like `FiltersRail` and `DeckBuilderPage`.
