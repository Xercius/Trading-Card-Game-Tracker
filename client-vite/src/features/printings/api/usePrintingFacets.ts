import { useQuery } from "@tanstack/react-query";
import { fetchCardFacets } from "@/features/cards/api/list";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";
import { cardsQueryKeys } from "@/features/cards/queryKeys";

export type PrintingFacets = {
  games: string[];
  sets: string[];
  rarities: string[];
  types: string[];
  arenas: string[];
  aspects: string[];
  traits: string[];
};

/**
 * Fetches available filter facet options based on current printing search filters.
 */
export function usePrintingFacets(filters: CardFilters) {
  const facetFilters = {
    games: filters.games.length > 0 ? filters.games : undefined,
    sets: filters.sets.length > 0 ? filters.sets : undefined,
    rarities: filters.rarities.length > 0 ? filters.rarities : undefined,
    types: filters.types.length > 0 ? filters.types : undefined,
    ...(filters.arenas?.length ? { arenas: filters.arenas } : {}),
    ...(filters.aspects?.length ? { aspects: filters.aspects } : {}),
    ...(filters.traits?.length ? { traits: filters.traits } : {}),
    q: filters.q || undefined,
    printingSearch: true,
  };

  return useQuery({
    queryKey: cardsQueryKeys.printingFacets(filters),
    queryFn: () => fetchCardFacets(facetFilters),
    select: (facets): PrintingFacets => ({
      games: facets.games.map((facet) => facet.value),
      sets: facets.sets.map((facet) => facet.value),
      rarities: facets.rarities.map((facet) => facet.value),
      types: facets.types.map((facet) => facet.value),
      arenas: (facets.arenas ?? []).map((facet) => facet.value),
      aspects: (facets.aspects ?? []).map((facet) => facet.value),
      traits: (facets.traits ?? []).map((facet) => facet.value),
    }),
    staleTime: 60_000,
  });
}
