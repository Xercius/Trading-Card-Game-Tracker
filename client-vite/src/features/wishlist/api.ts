import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import http from "@/lib/http";
import { queryKeys } from "@/lib/queryKeys";
import {
  collectionKeys,
  type CollectionItem,
  type Paged as CollectionPaged,
} from "@/features/collection/api";

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
};

export type WishlistQueryParams = {
  userId: number | null | undefined;
  page: number;
  pageSize: number;
  q: string;
  game: string;
  enabled?: boolean;
};

/**
 * Fetches the paginated wishlist for a user, filtered by search text and active game.
 */
export function useWishlistQuery({
  userId,
  page,
  pageSize,
  q,
  game,
  enabled = true,
}: WishlistQueryParams) {
  return useQuery<PagedWishlist<WishlistItemDto>>({
    queryKey: queryKeys.wishlist.list(userId, page, pageSize, q, game),
    queryFn: async () => {
      if (!userId) throw new Error("User not selected");
      const res = await http.get<PagedWishlist<WishlistItemDto>>(`user/${userId}/wishlist`, {
        params: { page, pageSize, name: q || undefined, game },
      });
      return res.data;
    },
    enabled: Boolean(userId) && enabled,
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

      queryClient.invalidateQueries({ queryKey: collectionKeys.all });
      queryClient.invalidateQueries({ queryKey: ["value", "summary"] });
        predicate: (query) =>
          query.queryKey.length === 2 &&
          typeof query.queryKey[1] === "object" &&
          query.queryKey[1] !== null,
      });
      queryClient.invalidateQueries({ queryKey: ["collection", "sets-summary"] });
      queryClient.invalidateQueries({ queryKey: ["collection", "set-detail"] });
      onRemovedFromSelection?.(snapshot.printingId);
    },
  });
}
