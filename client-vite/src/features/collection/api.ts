import { useMutation, useQuery, useQueryClient, keepPreviousData, type QueryKey } from "@tanstack/react-query";
import { api } from "@/lib/api";
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
  list: (params: Omit<CollectionQueryParams, "userId"> & { userId: number | null }) => [
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

async function fetchCollection(params: CollectionQueryParams) {
  const { page, pageSize = DEFAULT_PAGE_SIZE, filters } = params;
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
      await api.put(`collection/${variables.printingId}`, {
        ownedQty: variables.ownedQty,
        proxyQty: variables.proxyQty,
      });
      return variables;
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
      await api.patch("collection/bulk", { items: variables.items });
      return variables.items;
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
          deltaMap.set(item.printingId, { ownedDelta: item.ownedDelta, proxyDelta: item.proxyDelta });
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
