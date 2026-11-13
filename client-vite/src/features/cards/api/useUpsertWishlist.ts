import { useMutation } from "@tanstack/react-query";
import http from "@/lib/http";
import type { QuickAddVariables, WishlistQuickAddResponse } from "./types";

export function useUpsertWishlist() {
  return useMutation({
    mutationFn: async (variables: QuickAddVariables) => {
      const response = await http.post<WishlistQuickAddResponse>("wishlist/items", variables);
      return response.data;
    },
  });
}
