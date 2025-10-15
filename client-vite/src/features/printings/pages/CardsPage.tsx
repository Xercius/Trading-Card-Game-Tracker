import * as React from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useDebouncedCallback } from "use-debounce";
import { MultiSelect } from "../components/MultiSelect";
import { usePrintings } from "../api/usePrintings";
import { deriveFacets } from "../api/usePrintingFacets";
import { useCardFilters } from "@/features/cards/filters/useCardFilters";
import { PrintingCard } from "../components/PrintingCard";

export default function CardsPage() {
  const { filters, setFilters } = useCardFilters();
  const { data, isLoading, isError, error } = usePrintings(filters);
  const printings = data ?? [];
  const facets = React.useMemo(() => deriveFacets(printings), [printings]);

  const [searchInput, setSearchInput] = React.useState(filters.q ?? "");
  const lastUserInputRef = React.useRef<string>(filters.q ?? "");

  React.useEffect(() => {
    // Only sync from filters.q if it's different from what the user last typed
    // This handles browser back/forward while preserving user input during typing
    if (filters.q !== lastUserInputRef.current) {
      setSearchInput(filters.q ?? "");
      lastUserInputRef.current = filters.q ?? "";
    }
  }, [filters.q]);

  const setSearch = useDebouncedCallback((q: string) => {
    setFilters((prev) => ({ ...prev, q, page: 1 }));
  }, 250);

  React.useEffect(() => () => setSearch.cancel(), [setSearch]);

  const handleSearchChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const value = event.target.value;
    setSearchInput(value);
    lastUserInputRef.current = value;
    setSearch(value);
  };

  const clearFilters = React.useCallback(() => {
    setFilters((prev) => ({ ...prev, games: [], sets: [], rarities: [], page: 1 }));
  }, [setFilters]);

  const removeFilterValue = React.useCallback(
    (key: "games" | "sets" | "rarities", value: string) => {
      setFilters((prev) => ({
        ...prev,
        [key]: prev[key].filter(item => item !== value),
        page: 1,
      }));
    },
    [setFilters]
  );

  const activeFilters = React.useMemo(
    () => [
      { key: "games" as const, label: "Game", values: filters.games ?? [] },
      { key: "sets" as const, label: "Set", values: filters.sets ?? [] },
      { key: "rarities" as const, label: "Rarity", values: filters.rarities ?? [] },
    ],
    [filters.games, filters.sets, filters.rarities]
  );

  const hasActiveFilters = activeFilters.some(filter => filter.values.length > 0);

  return (
    <div className="p-4 md:p-6 space-y-4">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <input
          type="search"
          placeholder="Search name or text…"
          value={searchInput}
          onChange={handleSearchChange}
          className="w-full md:w-80 rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />
        <div className="flex flex-wrap gap-3">
          <MultiSelect
            label="Game"
            values={filters.games ?? []}
            options={facets.games}
            onChange={vals => setFilters(prev => ({ ...prev, games: vals, page: 1 }))}
          />
          <MultiSelect
            label="Set"
            values={filters.sets ?? []}
            options={facets.sets}
            placeholder="Search sets…"
            onChange={vals => setFilters(prev => ({ ...prev, sets: vals, page: 1 }))}
          />
          <MultiSelect
            label="Rarity"
            values={filters.rarities ?? []}
            options={facets.rarities}
            onChange={vals => setFilters(prev => ({ ...prev, rarities: vals, page: 1 }))}
          />
          <Button variant="ghost" onClick={clearFilters} disabled={!hasActiveFilters}>
            Clear filters
          </Button>
        </div>
      </div>

      {hasActiveFilters && (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="text-muted-foreground">Active filters:</span>
          {activeFilters.flatMap(filter =>
            filter.values.map(value => (
              <Badge key={`${filter.key}-${value}`} variant="outline" className="gap-1">
                <span>
                  {filter.label}: {value}
                </span>
                <button
                  type="button"
                  aria-label={`Remove ${filter.label} ${value}`}
                  className="ml-1 inline-flex items-center justify-center rounded-full px-1 text-base leading-none"
                  onClick={() => removeFilterValue(filter.key, value)}
                >
                  ×
                </button>
              </Badge>
            ))
          )}
          <Button variant="ghost" size="sm" onClick={clearFilters}>
            Clear all
          </Button>
        </div>
      )}

      {isLoading && <div className="text-sm text-muted-foreground">Loading printings…</div>}
      {isError && <div className="text-sm text-destructive">Error: {error?.message}</div>}

      {!isLoading && !isError && (
        <>
          <div className="text-sm text-muted-foreground">{printings.length} printings</div>
          <ul className="grid gap-3 grid-cols-[repeat(auto-fit,minmax(200px,1fr))]">
            {printings.map(p => (
              <li key={p.printingId}>
                <PrintingCard p={p} />
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
