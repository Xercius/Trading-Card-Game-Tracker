import { useMemo, useState } from "react";
import { useInfiniteQuery } from "@tanstack/react-query";
import VirtualizedCardGrid from "@/components/VirtualizedCardGrid";
import type { CardSummary } from "@/components/CardTile";
import { Button } from "@/components/ui/button";
import { fetchCardsPage } from "@/features/cards/api";
import FiltersRail from "@/features/cards/filters/FiltersRail";
import PillsBar from "@/features/cards/filters/PillsBar";
import { useCardFilters } from "@/features/cards/filters/useCardFilters";
import { useUser } from "@/state/useUser";

// Heuristic: smaller page on low-core devices to reduce memory pressure.
const PAGE_SIZE = (navigator?.hardwareConcurrency ?? 4) <= 4 ? 60 : 96;

export default function CardsPage() {
  const { userId } = useUser();
  const { filters, toQueryKey } = useCardFilters();
  const [isMobileFiltersOpen, setMobileFiltersOpen] = useState(false);

  const filterKey = toQueryKey();
  const queryKey = useMemo(() => ["cards", { userId, filters: filterKey }], [userId, filterKey]);

  const query = useInfiniteQuery({
    queryKey,
    initialPageParam: 0,
    queryFn: async ({ pageParam }) =>
      fetchCardsPage({
        q: filters.q,
        games: filters.games,
        sets: filters.sets,
        rarities: filters.rarities,
        skip: pageParam as number,
        take: PAGE_SIZE,
      }),
    getNextPageParam: (lastPage) => lastPage.nextSkip ?? null,
    staleTime: 60_000,
    enabled: !!userId,
  });

  const items: CardSummary[] = useMemo(
    () => query.data?.pages.flatMap(p => p.items) ?? [],
    [query.data]
  );

  const hasNoResults = !query.isFetching && items.length === 0;

  return (
    <div className="flex h-[calc(100vh-64px)] bg-background">
      <aside className="hidden w-72 shrink-0 border-r bg-background lg:block">
        <FiltersRail />
      </aside>
      <div className="flex flex-1 flex-col overflow-hidden">
        <div className="flex items-center justify-between gap-2 border-b px-3 py-2 lg:hidden">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setMobileFiltersOpen(true)}
            aria-label="Open filters"
          >
            Filters
          </Button>
        </div>
        <div className="flex-1 overflow-hidden px-3 pb-3 pt-2 lg:px-4 lg:pb-4 lg:pt-4">
          <div className="flex h-full flex-col">
            <PillsBar />
            {query.isError && (
              <div className="mb-3 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive-foreground">
                Error loading cards
              </div>
            )}
            {hasNoResults && (
              <div className="mb-3 rounded-md border p-4 text-sm">No cards found</div>
            )}
            <div className="mt-3 flex-1 overflow-hidden rounded-lg border bg-card">
              <VirtualizedCardGrid
                items={items}
                isFetchingNextPage={query.isFetchingNextPage}
                hasNextPage={query.hasNextPage}
                fetchNextPage={() => query.fetchNextPage()}
                onCardClick={(c) => {
                  console.debug("card", c.id);
                }}
                minTileWidth={220}
                overscan={(navigator?.hardwareConcurrency ?? 4) <= 4 ? 6 : 8}
                footerHeight={88}
              />
            </div>
          </div>
        </div>
      </div>
      {isMobileFiltersOpen && (
        <div className="fixed inset-0 z-50 flex lg:hidden" role="dialog" aria-modal="true">
          <button
            type="button"
            className="flex-1 bg-black/40"
            aria-label="Close filters overlay"
            onClick={() => setMobileFiltersOpen(false)}
          />
          <div className="h-full w-80 max-w-full bg-background shadow-xl">
            <FiltersRail onClose={() => setMobileFiltersOpen(false)} />
          </div>
        </div>
      )}
    </div>
  );
}
