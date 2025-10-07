import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { DEFAULT_PRICE_HISTORY_DAYS, PRICE_HISTORY_STALE_TIME_MS } from "@/lib/constants";
import type { PriceHistory, PricePoint } from "./types";

export function usePriceHistory(
  printingId: number | null,
  days = DEFAULT_PRICE_HISTORY_DAYS
) {
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
    staleTime: PRICE_HISTORY_STALE_TIME_MS,
  });
}
