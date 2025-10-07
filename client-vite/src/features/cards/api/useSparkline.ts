import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { QUERY_STALE_MS_VALUE_HISTORY } from "@/constants";
import type { ValuePoint } from "@/types/value";

export const DAYS_DEFAULT = 30;

export function useSparkline(cardId: number | null, days = DAYS_DEFAULT) {
  return useQuery({
    queryKey: ["card", "sparkline", cardId, days],
    enabled: Number.isFinite(cardId) && (cardId ?? 0) > 0,
    staleTime: QUERY_STALE_MS_VALUE_HISTORY,
    queryFn: async () => {
      const response = await api.get<ValuePoint[]>(`cards/${cardId}/sparkline`, {
        params: { days },
      });
      return response.data;
    },
    select: (points: ValuePoint[]) => points.slice().sort((a, b) => a.d.localeCompare(b.d)),
  });
}
