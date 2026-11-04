import {
  useMutation,
  useQuery,
  useQueryClient,
  keepPreviousData,
  type QueryKey,
} from "@tanstack/react-query";
import { api } from "@/lib/api";
import { toProblemDetailsError } from "@/lib/problemDetails";
import { DEFAULT_PAGE_SIZE, STALE_TIME_MS, MIN_QTY, MAX_QTY } from "@/constants";

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
    const response = await api.get<Paged<CollectionItem>>("collection", {
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

export function useCollectionQuery(params: CollectionQueryParams) {
  return useQuery({
    queryKey: collectionKeys.list(params),
    queryFn: () => {
      if (!params.userId) throw new Error("User not selected");
      return fetchCollection(params);
    },
    enabled: !!params.userId,
    staleTime: STALE_TIME_MS,
    placeholderData: keepPreviousData,
  });
}

type SetOwnedProxyVariables = {
  printingId: number;
  ownedQty: number;
  proxyQty: number;
};

type MutationContext = {
  previous?: Paged<CollectionItem>;
};

function clampQuantity(value: number) {
  if (!Number.isFinite(value)) return MIN_QTY;
  const intValue = Math.trunc(value);
  return Math.min(MAX_QTY, Math.max(MIN_QTY, intValue));
}

export function useSetOwnedProxyMutation(queryKey: QueryKey) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (variables: SetOwnedProxyVariables) => {
      try {
        await api.put(`collection/${variables.printingId}`, {
          ownedQty: variables.ownedQty,
          proxyQty: variables.proxyQty,
        });
        return variables;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    onMutate: async (variables) => {
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData<Paged<CollectionItem>>(queryKey);
      if (!previous) return {} satisfies MutationContext;

      const nextItems = previous.items.map((item) => {
        if (item.cardPrintingId !== variables.printingId) return item;
        const ownedQty = clampQuantity(variables.ownedQty);
        const proxyQty = clampQuantity(variables.proxyQty);
        return {
          ...item,
          quantityOwned: ownedQty,
          quantityProxyOwned: proxyQty,
          availability: ownedQty,
          availabilityWithProxies: ownedQty + proxyQty,
        } satisfies CollectionItem;
      });

      queryClient.setQueryData(queryKey, {
        ...previous,
        items: nextItems,
      });

      return { previous } satisfies MutationContext;
    },
    onError: (_error, _variables, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey });
    },
  });
}

type BulkUpdateVariables = {
  items: Array<{ printingId: number; ownedDelta: number; proxyDelta: number }>;
};

export function useBulkUpdateMutation(queryKey: QueryKey) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (variables: BulkUpdateVariables) => {
      try {
        await api.patch("collection/bulk", { items: variables.items });
        return variables.items;
      } catch (error) {
        throw toProblemDetailsError(error);
      }
    },
    onMutate: async (variables) => {
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData<Paged<CollectionItem>>(queryKey);
      if (!previous) return {} satisfies MutationContext;

      const deltaMap = new Map<number, { ownedDelta: number; proxyDelta: number }>();
      for (const item of variables.items) {
        const existing = deltaMap.get(item.printingId);
        if (existing) {
          existing.ownedDelta += item.ownedDelta;
          existing.proxyDelta += item.proxyDelta;
        } else {
          deltaMap.set(item.printingId, {
            ownedDelta: item.ownedDelta,
            proxyDelta: item.proxyDelta,
          });
        }
      }

      const nextItems = previous.items.map((item) => {
        const delta = deltaMap.get(item.cardPrintingId);
        if (!delta) return item;

        const ownedQty = clampQuantity(item.quantityOwned + delta.ownedDelta);
        const proxyQty = clampQuantity(item.quantityProxyOwned + delta.proxyDelta);
        return {
          ...item,
          quantityOwned: ownedQty,
          quantityProxyOwned: proxyQty,
          availability: ownedQty,
          availabilityWithProxies: ownedQty + proxyQty,
        } satisfies CollectionItem;
      });

      queryClient.setQueryData(queryKey, {
        ...previous,
        items: nextItems,
      });

      return { previous } satisfies MutationContext;
    },
    onError: (_error, _variables, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey });
    },
  });
}

export type UserCardQuantities = {
  cardPrintingId: number;
  quantityOwned: number;
  quantityProxyOwned: number;
  quantityWanted: number;
};

export async function fetchPrintingQuantities(printingId: number): Promise<UserCardQuantities> {
  try {
    const response = await api.get<UserCardQuantities>(`collection/${printingId}`);
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
        await api.post("collection", {
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
        await api.post("collection/batch-set", { items });
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

export async function fetchCollectionSetsSummary(game?: string): Promise<CollectionSetSummaryResponse> {
  try {
    const response = await api.get<CollectionSetSummaryResponse>("collection/sets-summary", {
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

