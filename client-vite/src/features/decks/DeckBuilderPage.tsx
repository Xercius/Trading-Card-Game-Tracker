import { useCallback, useMemo, useState } from "react";
import { useInfiniteQuery } from "@tanstack/react-query";
import CardTile, { type CardSummary } from "@/components/CardTile";
import VirtualizedCardGrid from "@/components/VirtualizedCardGrid";
import { Button } from "@/components/ui/button";
import { CardFiltersDrawer } from "@/features/printings/components/CardFiltersDrawer";
import { usePrintingFacets } from "@/features/printings/api/usePrintingFacets";
import PillsBar from "@/features/cards/filters/PillsBar";
import { useCardFilters } from "@/features/cards/filters/useCardFilters";
import { fetchPrintings } from "@/features/printings/api/printings";
import { buildPrintingQuery } from "@/features/printings/api/usePrintings";
import StickyDeckSidebar from "./StickyDeckSidebar";
import { DecklistExportDialog } from "./components/DecklistExportDialog";
import { DecklistImportDialog } from "./components/DecklistImportDialog";
import { PrintingPickerPopover } from "./components/PrintingPickerPopover";
import {
  useAddMissingToWishlistMutation,
  useDeckCardsWithAvailability,
  useDeckDetails,
  useDeckQuantityMutation,
  useDeckValueHistory,
  usePatchDeckCardMutation,
  useSwitchDeckCardPrintingMutation,
} from "./api";
import { useGame } from "@/state/useGame";
import { useIncludeProxies } from "@/hooks/useIncludeProxies";
import { pageSizeForDevice, overscanForDevice } from "@/lib/perf";
import { latestValue } from "@/lib/valueHistory";
import {
  CARD_IMAGE_DATA,
  CARD_NAME_DATA,
  DRAG_SOURCE_DATA,
  DRAG_SOURCE_GRID,
  PRINTING_ID_DATA,
} from "./constants";

const PAGE_SIZE = pageSizeForDevice(60, 96);
const EMPTY_DECK_VALUE_POINTS: { d: string; v: number | null }[] = [];

const normalizePrintingId = (printingId: CardSummary["id"] | null): number | null => {
  if (printingId == null) return null;
  const value = typeof printingId === "number" ? printingId : Number(printingId);
  return Number.isFinite(value) ? value : null;
};

function formatCurrency(value: number | null): string {
  if (value == null || Number.isNaN(value)) return "—";
  return `$${value.toFixed(2)}`;
}

type Props = {
  deckId: number;
};

export default function DeckBuilderPage({ deckId }: Props) {
  const { activeGame } = useGame();
  const [includeProxies, setIncludeProxies] = useIncludeProxies();
  const { filters, setFilters, clearAll } = useCardFilters();
  const [isFiltersOpen, setFiltersOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [exportOpen, setExportOpen] = useState(false);

  const deckDetailsQuery = useDeckDetails(deckId);
  const currentGame = deckDetailsQuery.data?.game || activeGame;
  const printingQuery = buildPrintingQuery({
    ...filters,
    games: [currentGame],
    pageSize: PAGE_SIZE,
    sort: filters.sort || "name",
  });
  const cardsQuery = useInfiniteQuery({
    queryKey: ["cards", "deck-builder", printingQuery],
    initialPageParam: 1,
    queryFn: ({ pageParam }) => fetchPrintings({ ...printingQuery, page: pageParam }),
    getNextPageParam: (lastPage, pages) =>
      lastPage.items.length > 0 && pages.length * PAGE_SIZE < lastPage.total
        ? pages.length + 1
        : undefined,
    staleTime: 60_000,
  });

  const items = useMemo<CardSummary[]>(
    () =>
      cardsQuery.data?.pages.flatMap((page) =>
        page.items.map((printing) => ({
          ...printing,
          id: printing.cardId,
          name: printing.cardName,
          primaryPrintingId: normalizePrintingId(printing.printingId),
          cardType: printing.cardTypes?.[0],
        }))
      ) ?? [],
    [cardsQuery.data]
  );

  const deckCardsQuery = useDeckCardsWithAvailability(deckId, includeProxies);
  const deckQuantityMutation = useDeckQuantityMutation(deckId, includeProxies);
  const patchDeckCardMutation = usePatchDeckCardMutation(deckId, includeProxies);
  const addMissingWishlistMutation = useAddMissingToWishlistMutation(deckId, includeProxies);
  const switchPrintingMutation = useSwitchDeckCardPrintingMutation(deckId);
  const deckValueHistoryQuery = useDeckValueHistory(deckId);
  const deckValuePoints = deckValueHistoryQuery.data ?? EMPTY_DECK_VALUE_POINTS;
  const deckLatestValue = useMemo(() => latestValue(deckValuePoints), [deckValuePoints]);

  const handleCardDragStart = useCallback(
    (event: React.DragEvent<HTMLDivElement>, card: CardSummary) => {
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
    },
    []
  );

  const handleQuantityChange = useCallback(
    (
      printingId: number,
      delta: number,
      meta?: {
        cardName?: string;
        imageUrl?: string | null;
        availability?: number;
        availabilityWithProxies?: number;
      }
    ) => {
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

  const renderCard = useCallback(
    (card: CardSummary) => {
      const printingId = normalizePrintingId(card.primaryPrintingId);
      const numericCardId = typeof card.id === "number" ? card.id : Number(card.id);
      return (
        <div
          className="relative h-full w-full"
          draggable={printingId != null}
          onDragStart={(event) => handleCardDragStart(event, card)}
        >
          <CardTile card={card} />
          {Number.isFinite(numericCardId) && numericCardId > 0 && printingId != null && (
            <div className="absolute bottom-2 right-2 z-10">
              <PrintingPickerPopover
                cardId={numericCardId}
                currentPrintingId={printingId}
                actionLabel="+ Printing"
                ariaLabel="Choose printing"
                onSelectPrinting={(targetId) =>
                  handleQuantityChange(targetId, 1, {
                    cardName: card.name,
                    imageUrl: card.imageUrl,
                  })
                }
              />
            </div>
          )}
        </div>
      );
    },
    [handleCardDragStart, handleQuantityChange]
  );

  const deckName = deckDetailsQuery.data?.name ?? "Deck";
  const sidebarRows = deckCardsQuery.data ?? [];
  const {
    data: availableFacets,
    isLoading: isFacetsLoading,
    isError: isFacetsError,
    refetch: refetchFacets,
  } = usePrintingFacets({ ...filters, games: [currentGame] });
  const hasActiveFilters = Boolean(
    filters.sets.length ||
    filters.rarities.length ||
    filters.types.length ||
    filters.arenas.length ||
    filters.aspects.length ||
    filters.traits.length ||
    filters.keywords.length ||
    filters.colorIdentities?.length ||
    filters.costs?.length ||
    filters.powers?.length ||
    filters.toughnesses?.length ||
    filters.loyalties?.length ||
    filters.upgradePowers?.length ||
    filters.upgradeHps?.length ||
    filters.hps?.length ||
    filters.stages?.length ||
    filters.specialTypes?.length ||
    filters.evolvesFrom ||
    filters.side ||
    filters.subTypes?.length ||
    filters.personas?.length ||
    filters.destinies?.length ||
    filters.deploys?.length ||
    filters.forfeits?.length ||
    filters.abilities?.length ||
    filters.lightSideIcons?.length ||
    filters.darkSideIcons?.length
  );

  return (
    <div className="flex h-[calc(100vh-64px)] bg-background">
      <div className="flex flex-1 flex-col overflow-hidden">
        <div className="flex items-center justify-between gap-2 border-b px-3 py-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setFiltersOpen(true)}
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
        game={currentGame}
        format={deckDetailsQuery.data?.format}
        side={deckDetailsQuery.data?.side}
        rows={sidebarRows}
        includeProxies={includeProxies}
        onIncludeProxiesChange={setIncludeProxies}
        onAdjustQuantity={handleQuantityChange}
        onSwitchPrinting={(fromId, toId) =>
          switchPrintingMutation.mutate({ fromPrintingId: fromId, toPrintingId: toId })
        }
        onAddMissingToWishlist={(printingId) => addMissingWishlistMutation.mutate(printingId)}
        isAddingMissing={addMissingWishlistMutation.isPending}
        onAdjustSideboard={(row, delta) =>
          patchDeckCardMutation.mutate({
            printingId: row.printingId,
            updates: { quantitySideboard: Math.max(0, (row.quantitySideboard ?? 0) + delta) },
          })
        }
        onToggleCommander={(row) =>
          patchDeckCardMutation.mutate({
            printingId: row.printingId,
            updates: { isCommander: !row.isCommander },
          })
        }
        onToggleStarting={(row) =>
          patchDeckCardMutation.mutate({
            printingId: row.printingId,
            updates: { isStarting: !row.isStarting },
          })
        }
        onMoveToIdea={(row) =>
          patchDeckCardMutation.mutate({
            printingId: row.printingId,
            updates: { quantityIdea: (row.quantityIdea ?? 0) + 1 },
          })
        }
        onAdjustIdea={(printingId, nextIdeaQty) =>
          patchDeckCardMutation.mutate({ printingId, updates: { quantityIdea: nextIdeaQty } })
        }
        onPromoteIdea={(row) =>
          patchDeckCardMutation.mutate({
            printingId: row.printingId,
            updates: {
              quantityInDeck: row.quantityInDeck + 1,
              quantityIdea: Math.max(0, (row.quantityIdea ?? 0) - 1),
            },
          })
        }
        onOpenImport={() => setImportOpen(true)}
        onOpenExport={() => setExportOpen(true)}
        isLoading={deckCardsQuery.isLoading}
        valueHistory={{
          points: deckValuePoints,
          isLoading: deckValueHistoryQuery.isLoading,
          isError: deckValueHistoryQuery.isError,
          latestValue: deckLatestValue,
        }}
        formatCurrency={formatCurrency}
      />

      <CardFiltersDrawer
        open={isFiltersOpen}
        onOpenChange={setFiltersOpen}
        filters={filters}
        setFilters={setFilters}
        availableFacets={
          availableFacets ?? {
            games: [currentGame],
            sets: [],
            rarities: [],
            types: [],
            arenas: [],
            aspects: [],
            traits: [],
            keywords: [],
          }
        }
        isFacetsLoading={isFacetsLoading}
        isFacetsError={isFacetsError}
        onRetryFacets={refetchFacets}
        onClearFilters={clearAll}
        hasActiveFilters={hasActiveFilters}
        game={currentGame}
      />
      {importOpen && (
        <DecklistImportDialog deckId={deckId} open={importOpen} onOpenChange={setImportOpen} />
      )}
      {exportOpen && (
        <DecklistExportDialog deckId={deckId} open={exportOpen} onOpenChange={setExportOpen} />
      )}
    </div>
  );
}
