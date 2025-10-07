import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { PrintingSummary } from "./types";

export function useCardPrintings(cardId: number) {
  return useQuery({
    queryKey: ["card", cardId, "printings"],
    enabled: Number.isFinite(cardId) && cardId > 0,
    queryFn: async () => {
      const response = await api.get<PrintingSummary[]>(`cards/${cardId}/printings`);
      return response.data;
    },
    staleTime: 120_000,
  });
}
