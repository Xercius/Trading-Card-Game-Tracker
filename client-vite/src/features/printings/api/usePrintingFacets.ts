import { useQuery } from "@tanstack/react-query";
import { fetchCardFacets } from "@/features/cards/api/list";
import { cardsQueryKeys } from "@/features/cards/queryKeys";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";

export type PrintingFacets = {
  games: string[];
  sets: string[];
  rarities: string[];
};

export function usePrintingFacets(filters: Pick<CardFilters, "q" | "games" | "sets" | "rarities">) {
  return useQuery({
    queryKey: cardsQueryKeys.facetOptions(filters),
    queryFn: () => fetchCardFacets(filters),
    select: (facets): PrintingFacets => ({
      games: facets.games.map((facet) => facet.value),
      sets: facets.sets.map((facet) => facet.value),
      rarities: facets.rarities.map((facet) => facet.value),
    }),
    staleTime: 60_000,
  });
}
