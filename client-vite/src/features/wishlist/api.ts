import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import http from "@/lib/http";
import { queryKeys } from "@/lib/queryKeys";
import {
  collectionKeys,
  type CollectionItem,
  type Paged as CollectionPaged,
} from "@/features/collection/api";

export type WishlistSourceDto = {
  deckId: number | null;
  deckName: string;
  quantity: number;
};

export type WishlistItemDto = {
  cardPrintingId: number;
  quantityWanted: number;
  cardId: number;
  cardName: string;
  game: string;
  set: string;
  number: string;
  rarity: string;
  style: string;
  imageUrl: string | null;
  sources?: WishlistSourceDto[];
};

export type PagedWishlist<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export type MoveToCollectionResponse = {
  printingId: number;
  wantedAfter: number;
  ownedAfter: number;
  proxyAfter: number;
  availability: number;
  availabilityWithProxies: number;
};

export type MoveVariables = {
  printingId: number;
  quantity: number;
  useProxy?: boolean;
  deckId?: number | null;
  sourceSpecified?: boolean;
};

export type WishlistQueryParams = {
  page: number;
  pageSize: number;
  q: string;
  game: string;
  enabled?: boolean;
};

/**
 * Fetches the paginated wishlist filtered by search text and active game.
 */
export function useWishlistQuery({ page, pageSize, q, game, enabled = true }: WishlistQueryParams) {
  return useQuery<PagedWishlist<WishlistItemDto>>({
    queryKey: queryKeys.wishlist.list(page, pageSize, q, game),
    queryFn: async () => {
      const res = await http.get<PagedWishlist<WishlistItemDto>>("wishlist", {
        params: { page, pageSize, name: q || undefined, game },
      });
      return res.data;
    },
    enabled,
    staleTime: 30_000,
  });
}

/**
 * Moves copies of a printing from the user's wishlist into their collection and updates cached queries.
 */
export function useMoveToCollectionMutation(
  wishlistQueryKey: ReturnType<typeof queryKeys.wishlist.list>,
  onRemovedFromSelection?: (printingId: number) => void
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (variables: MoveVariables) => {
      const response = await http.post<MoveToCollectionResponse>("wishlist/move-to-collection", {
        cardPrintingId: variables.printingId,
        quantity: variables.quantity,
        useProxy: variables.useProxy ?? false,
        deckId: variables.deckId ?? null,
        sourceSpecified: variables.sourceSpecified ?? false,
      });
      return response.data;
    },
    onSuccess: (snapshot) => {
      queryClient.setQueryData<PagedWishlist<WishlistItemDto>>(wishlistQueryKey, (existing) => {
        if (!existing) return existing;

        const present = existing.items.some((item) => item.cardPrintingId === snapshot.printingId);
        if (!present) return existing;

        if (snapshot.wantedAfter <= 0) {
          const nextItems = existing.items.filter(
            (item) => item.cardPrintingId !== snapshot.printingId
          );
          const nextTotal = Math.max(0, existing.total - 1);
          return { ...existing, items: nextItems, total: nextTotal };
        }

        const nextItems = existing.items.map((item) =>
          item.cardPrintingId === snapshot.printingId
            ? { ...item, quantityWanted: snapshot.wantedAfter }
            : item
        );
        return { ...existing, items: nextItems };
      });

      const collectionQueries = queryClient.getQueriesData<unknown>({
        queryKey: collectionKeys.all,
      });
      for (const [key, cachedValue] of collectionQueries) {
        if (
          !cachedValue ||
          typeof cachedValue !== "object" ||
          !Array.isArray((cachedValue as { items?: unknown }).items)
        ) {
          continue;
        }
        const value = cachedValue as CollectionPaged<CollectionItem>;
        const idx = value.items.findIndex((item) => item.cardPrintingId === snapshot.printingId);
        if (idx < 0) continue;

        const nextItems = value.items.map((item) =>
          item.cardPrintingId === snapshot.printingId
            ? {
                ...item,
                quantityOwned: snapshot.ownedAfter,
                quantityProxyOwned: snapshot.proxyAfter,
                availability: snapshot.availability,
                availabilityWithProxies: snapshot.availabilityWithProxies,
              }
            : item
        );
        queryClient.setQueryData(key, { ...value, items: nextItems });
      }

      queryClient.invalidateQueries({
        queryKey: collectionKeys.all,
        predicate: (query) =>
          query.queryKey.length === 2 &&
          typeof query.queryKey[1] === "object" &&
          query.queryKey[1] !== null,
      });
      queryClient.invalidateQueries({ queryKey: ["value", "summary"] });
      queryClient.invalidateQueries({ queryKey: ["collection", "sets-summary"] });
      queryClient.invalidateQueries({ queryKey: ["collection", "set-detail"] });
      queryClient.invalidateQueries({ queryKey: queryKeys.wishlist.all() });
      onRemovedFromSelection?.(snapshot.printingId);
    },
  });
}

export type UpsertWishlistSourceVariables = {
  cardPrintingId: number;
  quantityWanted: number;
  deckId: number | null;
};

/**
 * Updates the desired quantity for a specific wishlist source (General or a Deck).
 */
export function useUpsertWishlistSourceMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (variables: UpsertWishlistSourceVariables) => {
      await http.post("wishlist", {
        cardPrintingId: variables.cardPrintingId,
        quantityWanted: Math.max(0, variables.quantityWanted),
        deckId: variables.deckId,
      });
      return variables;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.wishlist.all() });
      queryClient.invalidateQueries({ queryKey: collectionKeys.all });
    },
  });
}
