import { useQuery } from "@tanstack/react-query";
import { useUser } from "@/context/useUser";
import http from "@/lib/http";

type GameSliceDto = { game: string; cents: number };
type CollectionSummaryDto = { totalCents: number; byGame: GameSliceDto[] };

export default function ValueHubPage() {
  const { userId } = useUser();

  const { data, isLoading, error } = useQuery<CollectionSummaryDto>({
    queryKey: ["value", userId],
    queryFn: async () => {
      const res = await http.get<CollectionSummaryDto>("value/collection/summary");
      return res.data;
    },
    enabled: !!userId,
  });

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading value summary</div>;
  if (!data) return <div className="p-4">No value data found</div>;

  const formattedTotal = (data.totalCents / 100).toLocaleString(undefined, {
    style: "currency",
    currency: "USD",
  });

  return (
    <div className="p-4">
      <h1 className="mb-2 text-xl font-semibold">Collection Value</h1>
      <p>Latest total: {formattedTotal}</p>
      <div className="mt-4">
        <h2 className="mb-2 text-lg font-medium">By Game</h2>
        {data.byGame.length === 0 ? (
          <p className="text-sm text-gray-500">No game breakdown available.</p>
        ) : (
          <ul className="space-y-1 text-sm text-gray-700">
            {data.byGame.map((slice) => {
              const formattedSliceTotal = (slice.cents / 100).toLocaleString(undefined, {
                style: 'currency',
                currency: 'USD',
              });

              return (
                <li key={slice.game} className="flex justify-between">
                  <span>{slice.game}</span>
                  <span>{formattedSliceTotal}</span>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}