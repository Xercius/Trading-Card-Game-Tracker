import { useMutation } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { QuickAddVariables, WishlistQuickAddResponse } from "./types";

export function useUpsertWishlist() {
  return useMutation({
    mutationFn: async (variables: QuickAddVariables) => {
      const response = await api.post<WishlistQuickAddResponse>("wishlist/items", variables);
      return response.data;
    },
  });
}
