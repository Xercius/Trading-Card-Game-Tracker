import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { QUERY_STALE_MS_VALUE_HISTORY } from "@/constants";
import type { ValuePoint } from "@/types/value";

const DEFAULT_DAYS = 90;

export function useCollectionValueHistory(days = DEFAULT_DAYS, enabled = true) {
  return useQuery({
    queryKey: ["collection", "value-history", days],
    enabled,
    staleTime: QUERY_STALE_MS_VALUE_HISTORY,
    queryFn: async () => {
      const response = await api.get<ValuePoint[]>("collection/value/history", {
        params: { days },
      });
      return response.data.slice().sort((a, b) => a.d.localeCompare(b.d));
    },
  });
}
