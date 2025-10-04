import { useMemo } from "react";
import { useInfiniteQuery } from "@tanstack/react-query";
import VirtualizedCardGrid from "@/components/VirtualizedCardGrid";
import type { CardSummary } from "@/components/CardTile";
import { fetchCardsPage } from "@/features/cards/api";
// If you have a useListQuery hook, reuse it. Otherwise read from URLSearchParams inline.
import { useSearchParams } from "react-router-dom";
import { useUser } from "@/context/useUser";

const PAGE_SIZE = 60; // tune per perf

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

  return (
    <div className="h-[calc(100vh-64px)] p-3">
      {query.isError && <div className="p-4 text-red-500">Error loading cards</div>}
      {!query.isFetching && items.length === 0 && <div className="p-4">No cards found</div>}
      <VirtualizedCardGrid
        items={items}
        isFetchingNextPage={query.isFetchingNextPage}
        hasNextPage={query.hasNextPage}
        fetchNextPage={() => query.fetchNextPage()}
        onCardClick={(c) => {
          // Navigate to card detail if you have a route like /cards/:id
          // e.g., useNavigate()(`/cards/${c.id}`)
          console.debug("card", c.id);
        }}
        minTileWidth={220}
      />
    </div>
  );
}
