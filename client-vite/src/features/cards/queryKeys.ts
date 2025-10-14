// Centralized query keys for cards feature
// This ensures stable, predictable query keys for TanStack Query

import type { CardFilters } from "./filters/useCardFilters";

export const cardsQueryKeys = {
  all: ["cards"] as const,
  
  lists: () => [...cardsQueryKeys.all, "list"] as const,
  
  list: (filters: Pick<CardFilters, "q" | "games" | "sets" | "rarities" | "page" | "pageSize" | "sort">) =>
    [
      ...cardsQueryKeys.lists(),
      {
        q: filters.q,
        games: filters.games,
        sets: filters.sets,
        rarities: filters.rarities,
        page: filters.page,
        pageSize: filters.pageSize,
        sort: filters.sort,
      },
    ] as const,
  
  facets: () => [...cardsQueryKeys.all, "facets"] as const,
  
  facetGames: () => [...cardsQueryKeys.facets(), "games"] as const,
  
  facetSets: (games: string[]) => 
    [...cardsQueryKeys.facets(), "sets", games.join("|")] as const,
  
  facetRarities: (games: string[]) => 
    [...cardsQueryKeys.facets(), "rarities", games.join("|")] as const,
};
