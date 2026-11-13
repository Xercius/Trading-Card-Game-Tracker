import { useQuery } from "@tanstack/react-query";
import http from "@/lib/http";
import { queryKeys } from "@/lib/queryKeys";
import type { CardDetail } from "./types";

export function useCardDetails(cardId: number) {
  return useQuery({
    queryKey: queryKeys.card(cardId),
    enabled: Number.isFinite(cardId) && cardId > 0,
    queryFn: async () => {
      const response = await http.get<CardDetail>(`cards/${cardId}`);
      return response.data;
    },
    staleTime: 60_000,
  });
}
