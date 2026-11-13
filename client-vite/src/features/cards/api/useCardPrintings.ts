import { useQuery } from "@tanstack/react-query";
import http from "@/lib/http";
import { queryKeys } from "@/lib/queryKeys";
import type { PrintingSummary } from "./types";

export function useCardPrintings(cardId: number) {
  return useQuery({
    queryKey: queryKeys.cardPrintings(cardId),
    enabled: Number.isFinite(cardId) && cardId > 0,
    queryFn: async () => {
      const response = await http.get<PrintingSummary[]>(`cards/${cardId}/printings`);
      return response.data;
    },
    staleTime: 120_000,
  });
}
