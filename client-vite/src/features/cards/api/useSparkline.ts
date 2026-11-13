import { useQuery } from "@tanstack/react-query";
import http from "@/lib/http";
import { QUERY_STALE_MS_VALUE_HISTORY } from "@/constants";
import { queryKeys } from "@/lib/queryKeys";
import type { ValuePoint } from "@/types/value";

export const DAYS_DEFAULT = 30;

export function useSparkline(cardId: number | null, days = DAYS_DEFAULT) {
  return useQuery({
    queryKey: queryKeys.sparkline(cardId ?? 0, days),
    enabled: Number.isFinite(cardId) && (cardId ?? 0) > 0,
    staleTime: QUERY_STALE_MS_VALUE_HISTORY,
    queryFn: async () => {
      const response = await http.get<ValuePoint[]>(`cards/${cardId}/sparkline`, {
        params: { days },
      });
      return response.data;
    },
    select: (points: ValuePoint[]) => points.slice().sort((a, b) => a.d.localeCompare(b.d)),
  });
}
