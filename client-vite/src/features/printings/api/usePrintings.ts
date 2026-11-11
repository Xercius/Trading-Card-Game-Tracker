import { useQuery } from "@tanstack/react-query";
import { fetchPrintings, type PrintingQuery, type PrintingsResult } from "./printings";
import { cardsQueryKeys } from "@/features/cards/queryKeys";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";

/**
 * Queries paginated card printings, applying any deck aspect budget before pagination.
 */
export function usePrintings(filters: CardFilters, availableAspects?: string[]) {
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
    ...(filters.traitMethod && filters.traitMethod !== "or"
      ? { traitMethod: filters.traitMethod }
      : {}),
    ...(filters.keywords?.length ? { keywords: filters.keywords } : {}),
    ...(filters.side ? { side: filters.side } : {}),
    ...(filters.personas?.length ? { personas: filters.personas } : {}),
    ...(filters.subTypes?.length ? { subTypes: filters.subTypes } : {}),
    ...(filters.destinies?.length ? { destinies: filters.destinies } : {}),
    ...(filters.deploys?.length ? { deploys: filters.deploys } : {}),
    ...(filters.forfeits?.length ? { forfeits: filters.forfeits } : {}),
    ...(filters.powers?.length ? { powers: filters.powers } : {}),
    ...(filters.abilities?.length ? { abilities: filters.abilities } : {}),
    ...(filters.lightSideIcons?.length ? { lightSideIcons: filters.lightSideIcons } : {}),
    ...(filters.darkSideIcons?.length ? { darkSideIcons: filters.darkSideIcons } : {}),
    ...(filters.stages?.length ? { stages: filters.stages } : {}),
    ...(filters.keywordMethod && filters.keywordMethod !== "or"
      ? { keywordMethod: filters.keywordMethod }
      : {}),
    ...(filters.sort ? { sort: filters.sort } : {}),
    pageSize,
    uniqueCards: true,
    ...(availableAspects !== undefined ? { availableAspects } : {}),
  };

  return useQuery<PrintingsResult, Error>({
    queryKey: [...cardsQueryKeys.list(filters), query],
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
