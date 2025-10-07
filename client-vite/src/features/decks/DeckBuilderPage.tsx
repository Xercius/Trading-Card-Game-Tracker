import { useCallback, useEffect, useMemo, useState } from "react";
import { useInfiniteQuery } from "@tanstack/react-query";
import CardTile, { type CardSummary } from "@/components/CardTile";
import VirtualizedCardGrid from "@/components/VirtualizedCardGrid";
import { Button } from "@/components/ui/button";
import FiltersRail from "@/features/cards/filters/FiltersRail";
import PillsBar from "@/features/cards/filters/PillsBar";
import { useCardFilters } from "@/features/cards/filters/useCardFilters";
import { fetchCardsPage } from "@/features/cards/api";
import StickyDeckSidebar from "./StickyDeckSidebar";
import { useDeckCardsWithAvailability, useDeckDetails, useDeckQuantityMutation } from "./api";
import { useUser } from "@/state/useUser";
import { useIncludeProxies } from "@/hooks/useIncludeProxies";
import { pageSizeForDevice, overscanForDevice } from "@/lib/perf";
import {
  CARD_IMAGE_DATA,
  CARD_NAME_DATA,
  DRAG_SOURCE_DATA,
  DRAG_SOURCE_GRID,
  PRINTING_ID_DATA,
} from "./constants";

const PAGE_SIZE = pageSizeForDevice(60, 96);

const normalizePrintingId = (printingId: CardSummary["primaryPrintingId"]): number | null => {
  if (printingId == null) return null;
  const value = typeof printingId === "number" ? printingId : Number(printingId);
  return Number.isFinite(value) ? value : null;
};

type Props = {
  deckId: number;
};

export default function DeckBuilderPage({ deckId }: Props) {
  const { userId } = useUser();
  const [includeProxies, setIncludeProxies] = useIncludeProxies();
  const { filters, toQueryKey } = useCardFilters();
  const [isMobileFiltersOpen, setMobileFiltersOpen] = useState(false);

  const filterKey = toQueryKey();
  const cardsQueryKey = useMemo(
    () => ["cards", { userId, filters: filterKey }],
    [userId, filterKey]
  );

  const cardsQuery = useInfiniteQuery({
    queryKey: cardsQueryKey,
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

  const items = useMemo(
    () => cardsQuery.data?.pages.flatMap((page) => page.items) ?? [],
    [cardsQuery.data]
  );

  const deckDetailsQuery = useDeckDetails(deckId);
  const deckCardsQuery = useDeckCardsWithAvailability(deckId, includeProxies);
  const deckQuantityMutation = useDeckQuantityMutation(deckId, includeProxies);

  const handleCardDragStart = useCallback((event: React.DragEvent<HTMLDivElement>, card: CardSummary) => {
    const printingId = normalizePrintingId(card.primaryPrintingId);
    if (printingId == null) {
      event.preventDefault();
      return;
    }
    event.dataTransfer.effectAllowed = "copy";
    event.dataTransfer.setData(PRINTING_ID_DATA, String(printingId));
    event.dataTransfer.setData(CARD_NAME_DATA, card.name);
    if (card.imageUrl) event.dataTransfer.setData(CARD_IMAGE_DATA, card.imageUrl);
    event.dataTransfer.setData(DRAG_SOURCE_DATA, DRAG_SOURCE_GRID);
  }, []);

  const renderCard = useCallback(
    (card: CardSummary) => (
      <div
        className="h-full w-full"
        draggable={normalizePrintingId(card.primaryPrintingId) != null}
        onDragStart={(event) => handleCardDragStart(event, card)}
      >
        <CardTile card={card} />
      </div>
    ),
    [handleCardDragStart]
  );

  const handleQuantityChange = useCallback(
    (printingId: number, delta: number, meta?: { cardName?: string; imageUrl?: string | null; availability?: number; availabilityWithProxies?: number }) => {
      if (!Number.isFinite(printingId) || delta === 0) return;
      deckQuantityMutation.mutate({
        printingId,
        qtyDelta: delta,
        cardName: meta?.cardName,
        imageUrl: meta?.imageUrl,
        initialAvailability: meta?.availability,
        initialAvailabilityWithProxies: meta?.availabilityWithProxies,
      });
    },
    [deckQuantityMutation]
  );

  useEffect(() => {
    if (!isMobileFiltersOpen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setMobileFiltersOpen(false);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [isMobileFiltersOpen]);

  const deckName = deckDetailsQuery.data?.name ?? "Deck";
  const sidebarRows = deckCardsQuery.data ?? [];

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

            {deckDetailsQuery.isError && (
              <div className="mb-3 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive-foreground">
                Error loading deck details
              </div>
            )}

            {deckCardsQuery.isError && (
              <div className="mb-3 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive-foreground">
                Error loading deck cards
              </div>
            )}

            {cardsQuery.isError && (
              <div className="mb-3 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive-foreground">
                Error loading cards
              </div>
            )}

            <div className="mt-3 flex-1 overflow-hidden rounded-lg border bg-card">
              <VirtualizedCardGrid
                items={items}
                isFetchingNextPage={cardsQuery.isFetchingNextPage}
                hasNextPage={cardsQuery.hasNextPage}
                fetchNextPage={() => cardsQuery.fetchNextPage()}
                minTileWidth={220}
                overscan={overscanForDevice(6, 8)}
                renderItem={renderCard}
              />
            </div>
          </div>
        </div>
      </div>

      <StickyDeckSidebar
        deckName={deckName}
        rows={sidebarRows}
        includeProxies={includeProxies}
        onIncludeProxiesChange={setIncludeProxies}
        onAdjustQuantity={handleQuantityChange}
        isLoading={deckCardsQuery.isLoading}
      />

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
