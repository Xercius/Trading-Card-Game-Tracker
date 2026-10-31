import { useQuery } from "@tanstack/react-query";
import { fetchPrintings, type PrintingQuery, type PrintingsResult } from "./printings";
import { cardsQueryKeys } from "@/features/cards/queryKeys";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";

/**
 * Queries paginated card printings matching the active card filters.
 */
export function usePrintings(filters: CardFilters) {
  const pageSize = Math.min(filters.pageSize, 500);
  const query: Omit<PrintingQuery, "page"> = {
    q: filters.q || undefined,
    game: filters.games.length > 0 ? filters.games : undefined,
    set: filters.sets.length > 0 ? filters.sets : undefined,
    rarity: filters.rarities.length > 0 ? filters.rarities : undefined,
    types: filters.types.length > 0 ? filters.types : undefined,
    ...(filters.arenas?.length ? { arenas: filters.arenas } : {}),
    ...(filters.aspects?.length ? { aspects: filters.aspects } : {}),
    ...(filters.aspectMethod && filters.aspectMethod !== "or"
      ? { aspectMethod: filters.aspectMethod }
      : {}),
    ...(filters.traits?.length ? { traits: filters.traits } : {}),
    ...(filters.sort ? { sort: filters.sort } : {}),
    pageSize,
    uniqueCards: true,
  };

  return useQuery<PrintingsResult, Error>({
    queryKey: cardsQueryKeys.list(filters),
    queryFn: async () => {
      const res = await fetchPrintings({ ...query, page: filters.page });
      if (Array.isArray(res)) {
        return { items: res, total: (res as unknown[]).length };
      }
      return res;
    },
    staleTime: 60_000,
  });
}
