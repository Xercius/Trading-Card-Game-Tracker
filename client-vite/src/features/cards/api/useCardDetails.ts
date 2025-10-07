import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { CardDetail } from "./types";

export function useCardDetails(cardId: number) {
  return useQuery({
    queryKey: ["card", cardId],
    enabled: Number.isFinite(cardId) && cardId > 0,
    queryFn: async () => {
      const response = await api.get<CardDetail>(`cards/${cardId}`);
      return response.data;
    },
    staleTime: 60_000,
  });
}
