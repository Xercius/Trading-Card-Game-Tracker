import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import http from "@/lib/http";
import { toProblemDetailsError } from "@/lib/problemDetails";
import { DEFAULT_PAGE_SIZE, STALE_TIME_MS } from "@/constants";

export type CollectionItem = {
  cardPrintingId: number;
  quantityOwned: number;
  quantityWanted: number;
  quantityProxyOwned: number;
  availability: number;
  availabilityWithProxies: number;
  cardId: number;
  cardName: string;
  game: string;
  set: string;
  number: string;
  rarity: string;
  style: string;
  imageUrl: string | null;
};

export type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export type CollectionFilters = {
  game?: string;
  set?: string;
  rarity?: string;
  q?: string;
};

export type CollectionQueryParams = {
  page: number;
  pageSize?: number;
  filters: CollectionFilters;
  includeProxies: boolean;
};

export const collectionKeys = {
  all: ["collection"] as const,
  list: (params: CollectionQueryParams) =>
    [
      "collection",
      {
        page: params.page,
        pageSize: params.pageSize ?? DEFAULT_PAGE_SIZE,
        filters: params.filters,
        includeProxies: params.includeProxies,
      },
    ] as const,
};

export async function fetchCollection(params: CollectionQueryParams) {
  const { page, pageSize = DEFAULT_PAGE_SIZE, filters } = params;
  try {
    const response = await http.get<Paged<CollectionItem>>("collection", {
      params: {
        page,
        pageSize,
        game: filters.game || undefined,
        set: filters.set || undefined,
        rarity: filters.rarity || undefined,
        name: filters.q || undefined,
      },
    });
    return response.data;
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

/** Fetch every page matching a collection query. */
export async function fetchAllCollection(params: CollectionQueryParams): Promise<CollectionItem[]> {
  const pageSize = 500;
  const firstPage = await fetchCollection({ ...params, page: 1, pageSize });
  const items = [...firstPage.items];

  for (let page = 2; items.length < firstPage.total; page += 1) {
    const nextPage = await fetchCollection({ ...params, page, pageSize });
    if (nextPage.items.length === 0) break;
    items.push(...nextPage.items);
  }

  return items;
}

export type UserCardQuantities = {
  cardPrintingId: number;
  quantityOwned: number;
  quantityProxyOwned: number;
  quantityWanted: number;
};

export async function fetchPrintingQuantities(printingId: number): Promise<UserCardQuantities> {
  try {
    const response = await http.get<UserCardQuantities>(`collection/${printingId}`);
    return response.data;
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

export function usePrintingQuantitiesQuery(printingId: number | null | undefined) {
  return useQuery({
    queryKey: ["collection", "quantities", printingId],
    queryFn: () => (printingId ? fetchPrintingQuantities(printingId) : null),
    enabled: typeof printingId === "number" && printingId > 0,
    staleTime: STALE_TIME_MS,
  });
}

export type SetCardQuantitiesVariables = {
  printingId: number;
  quantityOwned?: number;
  quantityProxyOwned?: number;
  quantityWanted?: number;
};

export function useSetCardQuantitiesMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ["collection", "set-quantities"],
    mutationFn: async (variables: SetCardQuantitiesVariables) => {
      try {
        await http.post("collection", {
          cardPrintingId: variables.printingId,
          quantityOwned: variables.quantityOwned ?? 0,
          quantityWanted: variables.quantityWanted ?? 0,
          quantityProxyOwned: variables.quantityProxyOwned ?? 0,
        });
        return variables;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    onMutate: async (variables) => {
      await queryClient.cancelQueries({ queryKey: collectionKeys.all });

      const quantitiesKey = ["collection", "quantities", variables.printingId] as const;
      const prevQuantities = queryClient.getQueryData<UserCardQuantities>(quantitiesKey);
      const prevSetDetails = queryClient.getQueriesData<CollectionItem[]>({
        queryKey: ["collection", "set-detail"],
      });

      const nextOwned = variables.quantityOwned ?? prevQuantities?.quantityOwned ?? 0;
      const nextProxy = variables.quantityProxyOwned ?? prevQuantities?.quantityProxyOwned ?? 0;
      const nextWanted = variables.quantityWanted ?? prevQuantities?.quantityWanted ?? 0;

      queryClient.setQueryData<UserCardQuantities>(quantitiesKey, {
        cardPrintingId: variables.printingId,
        quantityOwned: nextOwned,
        quantityProxyOwned: nextProxy,
        quantityWanted: nextWanted,
      });

      for (const [key, items] of prevSetDetails) {
        if (!Array.isArray(items)) continue;
        const idx = items.findIndex((item) => item.cardPrintingId === variables.printingId);
        if (idx >= 0) {
          const updated = [...items];
          const existing = updated[idx];
          const owned = variables.quantityOwned ?? existing.quantityOwned;
          const proxy = variables.quantityProxyOwned ?? existing.quantityProxyOwned;
          const wanted = variables.quantityWanted ?? existing.quantityWanted;
          updated[idx] = {
            ...existing,
            quantityOwned: owned,
            quantityProxyOwned: proxy,
            quantityWanted: wanted,
            availability: owned,
            availabilityWithProxies: owned + proxy,
          };
          queryClient.setQueryData<CollectionItem[]>(key, updated);
        } else {
          const inserted: CollectionItem = {
            cardPrintingId: variables.printingId,
            quantityOwned: nextOwned,
            quantityProxyOwned: nextProxy,
            quantityWanted: nextWanted,
            availability: nextOwned,
            availabilityWithProxies: nextOwned + nextProxy,
            cardId: 0,
            cardName: "",
            game: "",
            set: "",
            number: "",
            rarity: "",
            style: "",
            imageUrl: null,
          };
          queryClient.setQueryData<CollectionItem[]>(key, [...items, inserted]);
        }
      }

      return { prevQuantities, prevSetDetails, quantitiesKey };
    },
    onError: (_error, _variables, context) => {
      if (!context) return;
      queryClient.setQueryData(context.quantitiesKey, context.prevQuantities);
      for (const [key, data] of context.prevSetDetails) {
        queryClient.setQueryData(key, data);
      }
    },
    onSettled: (_data, _error, variables) => {
      const remaining = queryClient.isMutating({
        mutationKey: ["collection", "set-quantities"],
      });
      if (remaining <= 1) {
        queryClient.invalidateQueries({ queryKey: ["collection", "set-detail"] });
        queryClient.invalidateQueries({ queryKey: ["collection", "sets-summary"] });
        queryClient.invalidateQueries({ queryKey: ["collection", "value-history"] });
        if (variables.quantityWanted !== undefined) {
          queryClient.invalidateQueries({ queryKey: ["wishlist"] });
        }
      }
    },
  });
}

export type BatchSetItem = {
  cardPrintingId: number;
  quantityOwned: number;
  quantityProxyOwned: number;
  quantityWanted: number;
};

export function useBatchSetCollectionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (items: BatchSetItem[]) => {
      try {
        await http.post("collection/batch-set", { items });
        return items;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: collectionKeys.all });
      queryClient.invalidateQueries({ queryKey: ["wishlist"] });
    },
  });
}

export type CollectionSubsetSummaryDto = {
  treatmentId: number | null;
  treatmentName: string;
  totalCards: number;
  ownedCards: number;
  completionPercentage: number;
};

export type CollectionSetSummaryDto = {
  setId: number;
  setCode: string;
  setName: string;
  releaseYear: number | null;
  releasedAt: string | null;
  isVirtualSet: boolean;
  totalCards: number;
  ownedCards: number;
  completionPercentage: number;
  subsets: CollectionSubsetSummaryDto[];
};

export type CollectionSetSummaryResponse = {
  sets: CollectionSetSummaryDto[];
  totalUniqueCardsOwned: number;
  totalUniqueCardsInGame: number;
  overallCompletionPercentage: number;
};

export async function fetchCollectionSetsSummary(
  game?: string
): Promise<CollectionSetSummaryResponse> {
  try {
    const response = await http.get<CollectionSetSummaryResponse>("collection/sets-summary", {
      params: { game: game || undefined },
    });
    return response.data;
  } catch (error) {
    throw toProblemDetailsError(error);
  }
}

export function useCollectionSetsSummaryQuery(game?: string) {
  return useQuery({
    queryKey: ["collection", "sets-summary", game ?? "all"],
    queryFn: () => fetchCollectionSetsSummary(game),
    staleTime: STALE_TIME_MS,
  });
}
