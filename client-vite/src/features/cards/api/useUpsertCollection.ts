import { useMutation } from "@tanstack/react-query";
import http from "@/lib/http";
import type { CollectionQuickAddResponse, QuickAddVariables } from "./types";

export function useUpsertCollection() {
  return useMutation({
    mutationFn: async (variables: QuickAddVariables) => {
      const response = await http.post<CollectionQuickAddResponse>("collection/items", variables);
      return response.data;
    },
  });
}
