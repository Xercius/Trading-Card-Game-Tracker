import { useMutation } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { CollectionQuickAddResponse, QuickAddVariables } from "./types";

export function useUpsertCollection() {
  return useMutation({
    mutationFn: async (variables: QuickAddVariables) => {
      const response = await api.post<CollectionQuickAddResponse>("collection/items", variables);
      return response.data;
    },
  });
}
