import * as React from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useDebouncedCallback } from "use-debounce";
import { MultiSelect } from "../components/MultiSelect";
import { usePrintings } from "../api/usePrintings";
import { deriveFacets } from "../api/usePrintingFacets";
import { usePrintingSearch } from "../state/usePrintingSearch";
import { PrintingCard } from "../components/PrintingCard";

export default function CardsPage() {
  const [query, setQuery] = usePrintingSearch();
  const { data, isLoading, isError, error } = usePrintings(query);
  const printings = data ?? [];
  const facets = React.useMemo(() => deriveFacets(printings), [printings]);

  const [searchInput, setSearchInput] = React.useState(query.q ?? "");
  const lastUserInputRef = React.useRef<string>(query.q ?? "");

  React.useEffect(() => {
    // Only sync from query.q if it's different from what the user last typed
    // This handles browser back/forward while preserving user input during typing
    if (query.q !== lastUserInputRef.current) {
      setSearchInput(query.q ?? "");
      lastUserInputRef.current = query.q ?? "";
    }
  }, [query.q]);

  const setSearch = useDebouncedCallback((q: string) => {
    setQuery(prev => ({ ...prev, q, page: 1 }));
  }, 250);

  React.useEffect(() => () => setSearch.cancel(), [setSearch]);

  const handleSearchChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const value = event.target.value;
    setSearchInput(value);
    lastUserInputRef.current = value;
    setSearch(value);
  };

  const clearFilters = React.useCallback(() => {
    setQuery(prev => ({ ...prev, game: [], set: [], rarity: [], page: 1 }));
  }, [setQuery]);

  const removeFilterValue = React.useCallback(
    (key: "game" | "set" | "rarity", value: string) => {
      setQuery(prev => ({
        ...prev,
        [key]: (prev[key] ?? []).filter(item => item !== value),
        page: 1,
      }));
    },
    [setQuery]
  );

  const activeFilters = React.useMemo(
    () => [
      { key: "game" as const, label: "Game", values: query.game ?? [] },
      { key: "set" as const, label: "Set", values: query.set ?? [] },
      { key: "rarity" as const, label: "Rarity", values: query.rarity ?? [] },
    ],
    [query.game, query.set, query.rarity]
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
            values={query.game ?? []}
            options={facets.games}
            onChange={vals => setQuery(prev => ({ ...prev, game: vals, page: 1 }))}
          />
          <MultiSelect
            label="Set"
            values={query.set ?? []}
            options={facets.sets}
            placeholder="Search sets…"
            onChange={vals => setQuery(prev => ({ ...prev, set: vals, page: 1 }))}
          />
          <MultiSelect
            label="Rarity"
            values={query.rarity ?? []}
            options={facets.rarities}
            onChange={vals => setQuery(prev => ({ ...prev, rarity: vals, page: 1 }))}
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
          <ul className="grid gap-1 grid-cols-[repeat(auto-fit,minmax(200px,1fr))]">
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
