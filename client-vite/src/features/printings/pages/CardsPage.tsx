import * as React from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { MultiSelect } from "../components/MultiSelect";
import { usePrintings } from "../api/usePrintings";
import { usePrintingFacets } from "../api/usePrintingFacets";
import { useCardFilters } from "@/features/cards/filters/useCardFilters";
import { PrintingCard } from "../components/PrintingCard";
import { PrintingTableView } from "../components/PrintingTableView";
import CardModal from "@/features/cards/components/CardModal";
import type { PrintingListItem } from "../api/printings";

type ViewMode = "grid" | "table";
type FilterArrayKey = "games" | "sets" | "rarities" | "types" | "arenas" | "aspects" | "traits";
const EMPTY_PRINTINGS: PrintingListItem[] = [];

export default function CardsPage() {
  const { filters, setFilters } = useCardFilters();
  const {
    data,
    isLoading,
    isError,
    error,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    isFetchNextPageError,
  } = usePrintings(filters);
  const printings = data ?? EMPTY_PRINTINGS;
  const {
    data: facets,
    isLoading: isFacetsLoading,
    isError: isFacetsError,
    refetch: refetchFacets,
  } = usePrintingFacets(filters);
  const availableFacets = facets ?? {
    games: [],
    sets: [],
    rarities: [],
    types: [],
    arenas: [],
    aspects: [],
    traits: [],
  };

  const [viewMode, setViewMode] = React.useState<ViewMode>("grid");

  const [selectedPrinting, setSelectedPrinting] = React.useState<{
    cardId: number;
    printingId: number;
  } | null>(null);

  const handlePrintingClick = React.useCallback((p: PrintingListItem) => {
    const cardId = typeof p.cardId === "string" ? parseInt(p.cardId, 10) : p.cardId;
    const printingId = typeof p.printingId === "string" ? parseInt(p.printingId, 10) : p.printingId;
    setSelectedPrinting({ cardId, printingId });
  }, []);

  const clearFilters = React.useCallback(() => {
    setFilters((prev) => ({
      ...prev,
      games: [],
      sets: [],
      rarities: [],
      types: [],
      arenas: [],
      aspects: [],
      traits: [],
      page: 1,
    }));
  }, [setFilters]);

  const removeFilterValue = React.useCallback(
    (key: FilterArrayKey, value: string) => {
      setFilters((prev) => ({
        ...prev,
        [key]: prev[key].filter((item) => item !== value),
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
      { key: "types" as const, label: "Type", values: filters.types ?? [] },
      { key: "arenas" as const, label: "Arena", values: filters.arenas ?? [] },
      { key: "aspects" as const, label: "Aspect", values: filters.aspects ?? [] },
      { key: "traits" as const, label: "Trait", values: filters.traits ?? [] },
    ],
    [
      filters.games,
      filters.sets,
      filters.rarities,
      filters.types,
      filters.arenas,
      filters.aspects,
      filters.traits,
    ]
  );

  const hasActiveFilters = activeFilters.some((filter) => filter.values.length > 0);

  return (
    <div className="p-4 md:p-6 space-y-4">
      <div className="space-y-2">
        <div className="flex flex-wrap gap-3">
          <MultiSelect
            label="Game"
            values={filters.games ?? []}
            options={availableFacets.games}
            onChange={(vals) => setFilters((prev) => ({ ...prev, games: vals, page: 1 }))}
          />
          <MultiSelect
            label="Set"
            values={filters.sets ?? []}
            options={availableFacets.sets}
            placeholder="Search sets…"
            onChange={(vals) => setFilters((prev) => ({ ...prev, sets: vals, page: 1 }))}
          />
          <MultiSelect
            label="Rarity"
            values={filters.rarities ?? []}
            options={availableFacets.rarities}
            onChange={(vals) => setFilters((prev) => ({ ...prev, rarities: vals, page: 1 }))}
          />
          <MultiSelect
            label="Type"
            values={filters.types ?? []}
            options={availableFacets.types ?? []}
            placeholder="Search types…"
            onChange={(values) => setFilters((prev) => ({ ...prev, types: values, page: 1 }))}
          />
          <MultiSelect
            label="Arena"
            values={filters.arenas ?? []}
            options={availableFacets.arenas ?? []}
            placeholder="Search arenas…"
            onChange={(values) => setFilters((prev) => ({ ...prev, arenas: values, page: 1 }))}
          />
          <MultiSelect
            label="Aspects"
            values={filters.aspects ?? []}
            options={availableFacets.aspects ?? []}
            placeholder="Search aspects…"
            onChange={(values) => setFilters((prev) => ({ ...prev, aspects: values, page: 1 }))}
          />
          <MultiSelect
            label="Traits"
            values={filters.traits ?? []}
            options={availableFacets.traits ?? []}
            placeholder="Search traits…"
            onChange={(values) => setFilters((prev) => ({ ...prev, traits: values, page: 1 }))}
          />
          <Button variant="ghost" onClick={clearFilters} disabled={!hasActiveFilters}>
            Clear filters
          </Button>
        </div>
        {isFacetsLoading && <div className="text-sm text-muted-foreground">Loading filters…</div>}
        {isFacetsError && (
          <div className="flex items-center gap-2 text-sm text-destructive">
            <span>Unable to load filters.</span>
            <Button variant="outline" size="sm" onClick={() => void refetchFacets()}>
              Retry filters
            </Button>
          </div>
        )}
      </div>

      <div className="flex items-center gap-1" role="group" aria-label="View mode">
        <Button
          variant="outline"
          size="sm"
          onClick={() => setViewMode("grid")}
          aria-pressed={viewMode === "grid"}
          className={
            viewMode === "grid"
              ? "border-sky-400 bg-sky-700 text-white shadow-sm hover:bg-sky-800 hover:text-white"
              : "border-white/30 bg-transparent text-white/80 hover:border-white/50 hover:bg-white/10 hover:text-white"
          }
        >
          Grid
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={() => setViewMode("table")}
          aria-pressed={viewMode === "table"}
          className={
            viewMode === "table"
              ? "border-sky-400 bg-sky-700 text-white shadow-sm hover:bg-sky-800 hover:text-white"
              : "border-white/30 bg-transparent text-white/80 hover:border-white/50 hover:bg-white/10 hover:text-white"
          }
        >
          Table
        </Button>
      </div>

      {hasActiveFilters && (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="text-muted-foreground">Active filters:</span>
          {activeFilters.flatMap((filter) =>
            filter.values.map((value) => (
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
      {isError && !data && <div className="text-sm text-destructive">Error: {error?.message}</div>}

      {!isLoading && data && (
        <>
          <div className="text-sm text-muted-foreground">{printings.length} cards</div>
          {isFetchNextPageError && (
            <div className="text-sm text-destructive">
              Unable to load more printings. Please retry.
            </div>
          )}
          {viewMode === "grid" ? (
            <ul className="grid gap-1 grid-cols-[repeat(auto-fill,minmax(200px,320px))]">
              {printings.map((p) => (
                <li key={p.printingId}>
                  <PrintingCard p={p} onClick={handlePrintingClick} />
                </li>
              ))}
            </ul>
          ) : (
            <PrintingTableView printings={printings} onRowClick={handlePrintingClick} />
          )}
          {hasNextPage ? (
            <Button onClick={() => void fetchNextPage()} disabled={isFetchingNextPage}>
              {isFetchingNextPage
                ? "Loading more printings…"
                : isFetchNextPageError
                  ? "Retry loading printings"
                  : "Load more printings"}
            </Button>
          ) : (
            <div className="text-sm text-muted-foreground">
              {printings.length === 0 ? "No printings found." : "No more printings."}
            </div>
          )}
        </>
      )}

      {selectedPrinting && (
        <CardModal
          cardId={selectedPrinting.cardId}
          initialPrintingId={selectedPrinting.printingId}
          open={!!selectedPrinting}
          onOpenChange={(open) => {
            if (!open) setSelectedPrinting(null);
          }}
        />
      )}
    </div>
  );
}
