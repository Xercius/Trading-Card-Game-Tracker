import { useCallback, useMemo, useState } from "react";
import { useInfiniteQuery } from "@tanstack/react-query";
import VirtualizedCardGrid from "@/components/VirtualizedCardGrid";
import type { CardSummary } from "@/components/CardTile";
import { fetchCardsPage } from "@/features/cards/api";
import CardModal from "@/features/cards/components/CardModal";
// If you have a useListQuery hook, reuse it. Otherwise read from URLSearchParams inline.
import { useSearchParams } from "react-router-dom";
import { useUser } from "@/state/useUser";
import { pageSizeForDevice, overscanForDevice } from "@/lib/perf";

// Heuristic: smaller page on low-core devices to reduce memory pressure.
const PAGE_SIZE = pageSizeForDevice(60, 96);

export default function CardsPage() {
  const { userId } = useUser();
  const [params] = useSearchParams();
  const q = params.get("q") ?? "";
  const gameCsv = params.get("game") ?? "";
  const games = gameCsv ? gameCsv.split(",").filter(Boolean) : [];

  const gamesKey = useMemo(() => games.join(","), [games]);
  const queryKey = useMemo(() => ["cards", { userId, q, games: gamesKey }], [userId, q, gamesKey]);

  const query = useInfiniteQuery({
    queryKey,
    initialPageParam: 0,
    queryFn: async ({ pageParam }) =>
      fetchCardsPage({ q, games, skip: pageParam as number, take: PAGE_SIZE }),
    getNextPageParam: (lastPage) => lastPage.nextSkip ?? null,
    staleTime: 60_000,
    enabled: !!userId,
  });

  const items: CardSummary[] = useMemo(
    () => query.data?.pages.flatMap(p => p.items) ?? [],
    [query.data]
  );

  const [selectedCardId, setSelectedCardId] = useState<number | null>(null);
  const [selectedPrintingId, setSelectedPrintingId] = useState<number | null>(null);
  const [modalOpen, setModalOpen] = useState(false);

  const handleCardClick = useCallback((card: CardSummary) => {
    const parsedId = Number(card.id);
    if (!Number.isFinite(parsedId) || parsedId <= 0) return;
    setSelectedCardId(parsedId);
    setSelectedPrintingId(card.primaryPrintingId ?? null);
    setModalOpen(true);
  }, []);

  return (
    <div className="h-[calc(100vh-64px)] p-3">
      {query.isError && <div className="p-4 text-red-500">Error loading cards</div>}
      {!query.isFetching && items.length === 0 && <div className="p-4">No cards found</div>}
      <VirtualizedCardGrid
        items={items}
        isFetchingNextPage={query.isFetchingNextPage}
        hasNextPage={query.hasNextPage}
        fetchNextPage={() => query.fetchNextPage()}
        onCardClick={handleCardClick}
        minTileWidth={220}
        overscan={overscanForDevice(6, 8)}
        footerHeight={88}
      />
      {selectedCardId != null && (
        <CardModal
          cardId={selectedCardId}
          initialPrintingId={selectedPrintingId ?? undefined}
          open={modalOpen}
          onOpenChange={(next) => {
            setModalOpen(next);
            if (!next) {
              setSelectedCardId(null);
              setSelectedPrintingId(null);
            }
          }}
        />
      )}
    </div>
  );
}
