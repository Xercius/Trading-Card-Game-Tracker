// Centralized query keys for cards feature
// This ensures stable, predictable query keys for TanStack Query

import type { CardFilters } from "./filters/useCardFilters";

type OptionalExtraFilterFields = {
  arenas?: string[];
  aspects?: string[];
  aspectMethod?: "or" | "and" | "exclude";
  traits?: string[];
};

export const cardsQueryKeys = {
  all: ["cards"] as const,

  lists: () => [...cardsQueryKeys.all, "list"] as const,

  list: (
    filters: Pick<
      CardFilters,
      "q" | "games" | "sets" | "rarities" | "types" | "page" | "pageSize" | "sort"
    > &
      OptionalExtraFilterFields
  ) =>
    [
      ...cardsQueryKeys.lists(),
      {
        q: filters.q,
        games: filters.games,
        sets: filters.sets,
        rarities: filters.rarities,
        types: filters.types,
        arenas: filters.arenas ?? [],
        aspects: filters.aspects ?? [],
        aspectMethod: filters.aspectMethod ?? "or",
        traits: filters.traits ?? [],
        page: filters.page,
        pageSize: filters.pageSize,
        sort: filters.sort,
      },
    ] as const,

  facets: () => [...cardsQueryKeys.all, "facets"] as const,

  facetOptions: (
    filters: Pick<CardFilters, "q" | "games" | "sets" | "rarities" | "types"> &
      OptionalExtraFilterFields
  ) => [...cardsQueryKeys.facets(), filters] as const,

  facetGames: () => [...cardsQueryKeys.facets(), "games"] as const,

  facetSets: (games: string[]) => [...cardsQueryKeys.facets(), "sets", games.join("|")] as const,

  facetRarities: (games: string[]) =>
    [...cardsQueryKeys.facets(), "rarities", games.join("|")] as const,

  printingFacets: (
    filters: Pick<CardFilters, "q" | "games" | "sets" | "rarities" | "types"> &
      OptionalExtraFilterFields
  ) => [...cardsQueryKeys.facets(), "printings", filters] as const,
};
