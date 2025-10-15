import { useQuery } from "@tanstack/react-query";
import { fetchPrintings, type PrintingQuery, type PrintingListItem } from "./printings";
import { cardsQueryKeys } from "@/features/cards/queryKeys";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";

export function usePrintings(filters: CardFilters) {
  const query: PrintingQuery = {
    q: filters.q || undefined,
    game: filters.games.length > 0 ? filters.games : undefined,
    set: filters.sets.length > 0 ? filters.sets : undefined,
    rarity: filters.rarities.length > 0 ? filters.rarities : undefined,
    page: filters.page,
    pageSize: filters.pageSize,
  };

  return useQuery<PrintingListItem[], Error>({
    queryKey: cardsQueryKeys.list(filters),
    queryFn: () => fetchPrintings(query),
    staleTime: 60_000,
    placeholderData: (previousData) => previousData,
  });
}
