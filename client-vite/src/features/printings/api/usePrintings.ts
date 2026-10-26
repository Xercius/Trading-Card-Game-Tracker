import { useInfiniteQuery } from "@tanstack/react-query";
import { fetchPrintings, type PrintingQuery, type PrintingListItem } from "./printings";
import { cardsQueryKeys } from "@/features/cards/queryKeys";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";

export function usePrintings(filters: CardFilters) {
  const pageSize = Math.min(filters.pageSize, 500);
  const query: Omit<PrintingQuery, "page"> = {
    q: filters.q || undefined,
    game: filters.games.length > 0 ? filters.games : undefined,
    set: filters.sets.length > 0 ? filters.sets : undefined,
    rarity: filters.rarities.length > 0 ? filters.rarities : undefined,
    types: filters.types.length > 0 ? filters.types : undefined,
    pageSize,
    uniqueCards: true,
  };

  return useInfiniteQuery<
    PrintingListItem[],
    Error,
    PrintingListItem[],
    ReturnType<typeof cardsQueryKeys.list>,
    number
  >({
    queryKey: cardsQueryKeys.list(filters),
    queryFn: ({ pageParam }) => fetchPrintings({ ...query, page: pageParam }),
    initialPageParam: filters.page,
    getNextPageParam: (lastPage, allPages) =>
      lastPage.length === pageSize ? filters.page + allPages.length : undefined,
    select: (result) => {
      const seen = new Set<PrintingListItem["printingId"]>();
      return result.pages.flat().filter((printing) => {
        if (seen.has(printing.printingId)) return false;
        seen.add(printing.printingId);
        return true;
      });
    },
    staleTime: 60_000,
  });
}
