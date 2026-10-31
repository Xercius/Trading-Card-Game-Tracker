import * as React from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { usePrintings } from "../api/usePrintings";
import { usePrintingFacets } from "../api/usePrintingFacets";
import { useCardFilters } from "@/features/cards/filters/useCardFilters";
import { useGame } from "@/state/useGame";
import { isHorizontalPrinting } from "../utils/cardOrientation";
import { PrintingCard } from "../components/PrintingCard";
import { PrintingTableView } from "../components/PrintingTableView";
import { CardPagination } from "../components/CardPagination";
import { CardFiltersDrawer } from "../components/CardFiltersDrawer";
import CardModal from "@/features/cards/components/CardModal";
import type { PrintingListItem } from "../api/printings";

type ViewMode = "grid" | "table";
type FilterArrayKey = "games" | "sets" | "rarities" | "types" | "arenas" | "aspects" | "traits";
const EMPTY_PRINTINGS: PrintingListItem[] = [];

const MIN_CARD_SIZE = 120;
const MAX_CARD_SIZE = 320;
const DEFAULT_CARD_SIZE = 180;
const STORAGE_KEY_CARD_SIZE = "cards-grid-card-size";

type GridEntry =
  | { type: "single"; card: PrintingListItem; key: string }
  | { type: "pair"; top: PrintingListItem; bottom: PrintingListItem; key: string };

function FilterIcon({ className = "" }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      className={`h-4 w-4 ${className}`}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
    </svg>
  );
}

export default function CardsPage() {
  const { activeGame } = useGame();
  const { filters, setFilters } = useCardFilters();

  const previousGameRef = React.useRef(activeGame);
  React.useEffect(() => {
    if (previousGameRef.current !== activeGame) {
      previousGameRef.current = activeGame;
      setFilters((prev) => ({
        ...prev,
        games: [activeGame],
        page: 1,
      }));
    }
  }, [activeGame, setFilters]);

  const previousQRef = React.useRef(filters.q);
  React.useEffect(() => {
    if (previousQRef.current !== filters.q) {
      previousQRef.current = filters.q;
      if (filters.page !== 1) {
        setFilters((prev) => ({
          ...prev,
          page: 1,
        }));
      }
    }
  }, [filters.q, filters.page, setFilters]);

  const [viewMode, setViewMode] = React.useState<ViewMode>("grid");
  const effectivePageSize = viewMode === "table" ? 200 : filters.pageSize || 20;

  const scopedFilters = React.useMemo(
    () => ({
      ...filters,
      games: [activeGame],
      sort: filters.sort || "name",
      pageSize: effectivePageSize,
    }),
    [filters, activeGame, effectivePageSize]
  );

  const { data, isLoading, isError, error } = usePrintings(scopedFilters);

  const printings = (
    data && "items" in data ? data.items : Array.isArray(data) ? data : EMPTY_PRINTINGS
  ) as PrintingListItem[];
  const total =
    data && "total" in data ? data.total : Array.isArray(data) ? (data as unknown[]).length : 0;
  const totalPages = Math.max(1, Math.ceil(total / effectivePageSize));

  React.useEffect(() => {
    if (total > 0 && filters.page > totalPages) {
      setFilters((prev) => ({
        ...prev,
        page: 1,
      }));
    }
  }, [total, totalPages, filters.page, setFilters]);

  const handlePageChange = React.useCallback(
    (newPage: number) => {
      setFilters((prev) => ({
        ...prev,
        page: newPage,
      }));
    },
    [setFilters]
  );

  const handlePageSizeChange = React.useCallback(
    (newSize: number) => {
      setFilters((prev) => ({
        ...prev,
        pageSize: newSize,
        page: 1,
      }));
    },
    [setFilters]
  );

  const {
    data: facets,
    isLoading: isFacetsLoading,
    isError: isFacetsError,
    refetch: refetchFacets,
  } = usePrintingFacets(scopedFilters);
  const availableFacets = facets ?? {
    games: [],
    sets: [],
    rarities: [],
    types: [],
    arenas: [],
    aspects: [],
    traits: [],
  };

  const [isFilterDrawerOpen, setIsFilterDrawerOpen] = React.useState(false);

  const [cardSize, setCardSize] = React.useState<number>(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY_CARD_SIZE);
      if (saved) {
        const parsed = Number(saved);
        if (!Number.isNaN(parsed) && parsed >= MIN_CARD_SIZE && parsed <= MAX_CARD_SIZE) {
          return parsed;
        }
      }
    } catch {
      // Ignore localStorage access failures
    }
    return DEFAULT_CARD_SIZE;
  });

  const handleCardSizeChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const newSize = Number(event.target.value);
    setCardSize(newSize);
    try {
      localStorage.setItem(STORAGE_KEY_CARD_SIZE, String(newSize));
    } catch {
      // Ignore localStorage access failures
    }
  };

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
      games: [activeGame],
      sets: [],
      rarities: [],
      types: [],
      arenas: [],
      aspects: [],
      aspectMethod: "or",
      traits: [],
      page: 1,
    }));
  }, [setFilters, activeGame]);

  const removeFilterValue = React.useCallback(
    (key: FilterArrayKey, value: string) => {
      setFilters((prev) => {
        const nextList = prev[key].filter((item) => item.toLowerCase() !== value.toLowerCase());
        return {
          ...prev,
          [key]: nextList,
          ...(key === "aspects" && nextList.length === 0 ? { aspectMethod: "or" as const } : {}),
          page: 1,
        };
      });
    },
    [setFilters]
  );

  const groupedAspects = React.useMemo(() => {
    if (!filters.aspects?.length) return [];
    const counts: Record<string, number> = {};
    for (const a of filters.aspects) {
      const lower = a.toLowerCase();
      counts[lower] = (counts[lower] || 0) + 1;
    }
    const result: { display: string; value: string }[] = [];
    if (counts["no-aspect"]) {
      result.push({ display: "No Aspect", value: "no-aspect" });
    }
    const knownAspects = ["Vigilance", "Command", "Aggression", "Cunning", "Heroism", "Villainy"];
    for (const name of knownAspects) {
      const count = counts[name.toLowerCase()];
      if (count === 1) result.push({ display: name, value: name });
      else if (count > 1) result.push({ display: `${name} ×${count}`, value: name });
    }
    for (const [key, count] of Object.entries(counts)) {
      if (key === "no-aspect" || knownAspects.some((k) => k.toLowerCase() === key)) continue;
      const capital = key.charAt(0).toUpperCase() + key.slice(1);
      if (count === 1) result.push({ display: capital, value: key });
      else if (count > 1) result.push({ display: `${capital} ×${count}`, value: key });
    }
    return result;
  }, [filters.aspects]);

  const activeFilters = React.useMemo(
    () => [
      {
        key: "sets" as const,
        label: "Set",
        items: (filters.sets ?? []).map((v) => ({ display: v, value: v })),
      },
      {
        key: "rarities" as const,
        label: "Rarity",
        items: (filters.rarities ?? []).map((v) => ({ display: v, value: v })),
      },
      {
        key: "types" as const,
        label: "Type",
        items: (filters.types ?? []).map((v) => ({ display: v, value: v })),
      },
      {
        key: "arenas" as const,
        label: "Arena",
        items: (filters.arenas ?? []).map((v) => ({ display: v, value: v })),
      },
      {
        key: "aspects" as const,
        label:
          filters.aspectMethod === "exclude"
            ? "Exclude Aspect"
            : filters.aspectMethod === "and"
              ? "Aspect (AND)"
              : "Aspect",
        items: groupedAspects,
      },
      {
        key: "traits" as const,
        label: "Trait",
        items: (filters.traits ?? []).map((v) => ({ display: v, value: v })),
      },
    ],
    [
      filters.sets,
      filters.rarities,
      filters.types,
      filters.arenas,
      filters.aspectMethod,
      groupedAspects,
      filters.traits,
    ]
  );

  const hasActiveFilters = activeFilters.some((filter) => filter.items.length > 0);
  const activeFilterCount = activeFilters.reduce((sum, filter) => sum + filter.items.length, 0);

  const gridEntries = React.useMemo<GridEntry[]>(() => {
    const hasVertical = printings.some((p) => !isHorizontalPrinting(p));
    if (!hasVertical) {
      return printings.map((p) => ({
        type: "single",
        card: p,
        key: String(p.printingId),
      }));
    }

    const items: GridEntry[] = [];
    for (let i = 0; i < printings.length; i++) {
      const current = printings[i];
      const next = printings[i + 1];

      if (isHorizontalPrinting(current) && next && isHorizontalPrinting(next)) {
        items.push({
          type: "pair",
          top: current,
          bottom: next,
          key: `${current.printingId}-${next.printingId}`,
        });
        i++; // Skip the next card since it's paired with current
      } else {
        items.push({
          type: "single",
          card: current,
          key: String(current.printingId),
        });
      }
    }
    return items;
  }, [printings]);

  return (
    <div className="space-y-4 p-4 md:p-6">
      {/* Top Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-1" role="group" aria-label="View mode">
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                setViewMode("grid");
                setFilters((prev) => ({ ...prev, page: 1 }));
              }}
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
              onClick={() => {
                setViewMode("table");
                setFilters((prev) => ({ ...prev, page: 1 }));
              }}
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

          {viewMode === "grid" && (
            <div className="flex items-center gap-2 border-l border-white/20 pl-3">
              <svg
                aria-hidden="true"
                className="h-3.5 w-3.5 text-muted-foreground"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <rect x="5" y="3" width="14" height="18" rx="2" />
              </svg>
              <input
                type="range"
                min={MIN_CARD_SIZE}
                max={MAX_CARD_SIZE}
                step={4}
                value={cardSize}
                onChange={handleCardSizeChange}
                aria-label="Card size"
                className="h-1.5 w-24 sm:w-28 cursor-pointer appearance-none rounded-lg bg-white/20 accent-sky-500 hover:accent-sky-400"
              />
              <svg
                aria-hidden="true"
                className="h-5 w-5 text-muted-foreground"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <rect x="4" y="2" width="16" height="20" rx="2" />
              </svg>
            </div>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="outline"
            onClick={() => setIsFilterDrawerOpen(true)}
            aria-expanded={isFilterDrawerOpen}
            aria-controls="card-filters-drawer-title"
            className="flex items-center gap-2"
          >
            <FilterIcon />
            <span>Filters</span>
            {activeFilterCount > 0 && (
              <Badge variant="secondary" className="ml-1 px-1.5 py-0 text-xs font-semibold">
                {activeFilterCount}
              </Badge>
            )}
          </Button>

          <CardPagination
            page={filters.page}
            totalPages={totalPages}
            pageSize={effectivePageSize}
            onPageChange={handlePageChange}
            onPageSizeChange={handlePageSizeChange}
            isTableView={viewMode === "table"}
          />
        </div>
      </div>

      {hasActiveFilters && (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="text-muted-foreground">Active filters:</span>
          {activeFilters.flatMap((filter) =>
            filter.items.map((item) => (
              <Badge key={`${filter.key}-${item.value}`} variant="outline" className="gap-1">
                <span>
                  {filter.label}: {item.display}
                </span>
                <button
                  type="button"
                  aria-label={`Remove ${filter.label} ${item.display}`}
                  className="ml-1 inline-flex items-center justify-center rounded-full px-1 text-base leading-none"
                  onClick={() => removeFilterValue(filter.key, item.value)}
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
          {viewMode === "grid" ? (
            <ul
              className="grid gap-1"
              style={{
                gridTemplateColumns: `repeat(auto-fill, minmax(${cardSize}px, 1fr))`,
              }}
            >
              {gridEntries.map((entry) => {
                if (entry.type === "pair") {
                  return (
                    <li key={entry.key} className="flex flex-col gap-1">
                      <PrintingCard p={entry.top} onClick={handlePrintingClick} />
                      <PrintingCard p={entry.bottom} onClick={handlePrintingClick} />
                    </li>
                  );
                }
                return (
                  <li key={entry.key}>
                    <PrintingCard p={entry.card} onClick={handlePrintingClick} />
                  </li>
                );
              })}
            </ul>
          ) : (
            <PrintingTableView printings={printings} onRowClick={handlePrintingClick} />
          )}
          {printings.length === 0 ? (
            <div className="text-sm text-muted-foreground">No printings found.</div>
          ) : (
            <div className="flex justify-end pt-4 pb-8">
              <CardPagination
                page={filters.page}
                totalPages={totalPages}
                pageSize={effectivePageSize}
                onPageChange={handlePageChange}
                onPageSizeChange={handlePageSizeChange}
                isTableView={viewMode === "table"}
              />
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

      <CardFiltersDrawer
        open={isFilterDrawerOpen}
        onOpenChange={setIsFilterDrawerOpen}
        filters={filters}
        setFilters={setFilters}
        availableFacets={availableFacets}
        isFacetsLoading={isFacetsLoading}
        isFacetsError={isFacetsError}
        onRetryFacets={() => void refetchFacets()}
        onClearFilters={clearFilters}
        hasActiveFilters={hasActiveFilters}
      />
    </div>
  );
}
