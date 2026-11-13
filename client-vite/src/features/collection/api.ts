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
  userId: number | null;
  page: number;
  pageSize?: number;
  filters: CollectionFilters;
  includeProxies: boolean;
};

export const collectionKeys = {
  all: ["collection"] as const,
  list: (params: Omit<CollectionQueryParams, "userId"> & { userId: number | null }) =>
    [
      "collection",
      {
        userId: params.userId,
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
    onSuccess: (variables) => {
      queryClient.setQueryData(
        ["collection", "quantities", variables.printingId],
        (existing: UserCardQuantities | undefined) => ({
          cardPrintingId: variables.printingId,
          quantityOwned: variables.quantityOwned ?? existing?.quantityOwned ?? 0,
          quantityProxyOwned: variables.quantityProxyOwned ?? existing?.quantityProxyOwned ?? 0,
          quantityWanted: variables.quantityWanted ?? existing?.quantityWanted ?? 0,
        })
      );
      queryClient.invalidateQueries({ queryKey: collectionKeys.all });
      queryClient.invalidateQueries({ queryKey: ["wishlist"] });
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
