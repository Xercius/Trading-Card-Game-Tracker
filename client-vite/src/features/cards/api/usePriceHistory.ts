import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { PriceHistory, PricePoint } from "./types";

export function usePriceHistory(printingId: number | null, days = 30) {
  return useQuery({
    queryKey: ["price", printingId, days],
    enabled: Number.isFinite(printingId) && (printingId ?? 0) > 0,
    queryFn: async () => {
      const response = await api.get<PriceHistory>(`prices/${printingId}/history`, {
        params: { days },
      });
      return response.data.points;
    },
    select: (points: PricePoint[]) => points.slice().sort((a, b) => a.d.localeCompare(b.d)),
    staleTime: 5 * 60_000,
  });
}
