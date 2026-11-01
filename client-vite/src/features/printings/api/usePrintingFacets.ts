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
  keywords: string[];
  personas?: string[];
  subTypes?: string[];
  destinies?: string[];
  deploys?: string[];
  forfeits?: string[];
  powers?: string[];
  abilities?: string[];
  lightSideIcons?: string[];
  darkSideIcons?: string[];
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
    ...(filters.keywordMethod && filters.keywordMethod !== "or"
      ? { keywordMethod: filters.keywordMethod }
      : {}),
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
      keywords: (facets.keywords ?? []).map((facet) => facet.value),
      personas: (facets.personas ?? []).map((facet) => facet.value),
      subTypes: (facets.subTypes ?? []).map((facet) => facet.value),
      destinies: (facets.destinies ?? []).map((facet) => facet.value),
      deploys: (facets.deploys ?? []).map((facet) => facet.value),
      forfeits: (facets.forfeits ?? []).map((facet) => facet.value),
      powers: (facets.powers ?? []).map((facet) => facet.value),
      abilities: (facets.abilities ?? []).map((facet) => facet.value),
      lightSideIcons: (facets.lightSideIcons ?? []).map((facet) => facet.value),
      darkSideIcons: (facets.darkSideIcons ?? []).map((facet) => facet.value),
    }),
    staleTime: 60_000,
  });
}
