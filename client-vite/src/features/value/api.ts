import { queryOptions } from "@tanstack/react-query";
import http from "@/lib/http";
import { queryKeys } from "@/lib/queryKeys";

export interface GameSliceDto {
  game: string;
  cents: number;
}

export interface CollectionSummaryDto {
  totalCents: number;
  byGame: GameSliceDto[];
}

/** Fetches the aggregate collection value summary, optionally filtered by game. */
export async function fetchCollectionValueSummary(
  q: string,
  game: string
): Promise<CollectionSummaryDto> {
  const res = await http.get<CollectionSummaryDto>("values/collection/summary", {
    params: { q, game },
  });
  return res.data;
}

/** Query options for collection value summary with a 1-minute stale time. */
export function collectionValueSummaryQueryOptions(
  userId: number | null | undefined,
  q: string,
  game: string
) {
  return queryOptions({
    queryKey: [...queryKeys.value.summary(game), userId, q] as const,
    queryFn: () => fetchCollectionValueSummary(q, game),
    enabled: Boolean(userId),
    staleTime: 60_000,
  });
}
