import { useQuery } from "@tanstack/react-query";
import { useUser } from "@/state/useUser";
import { useGame } from "@/state/useGame";
import { useQueryState } from "@/hooks/useQueryState";
import { collectionValueSummaryQueryOptions } from "@/features/value/api";

export default function ValueHubPage() {
  const { userId } = useUser();
  const [q] = useQueryState("q", "");
  const { activeGame } = useGame();

  const { data, isLoading, error } = useQuery(
    collectionValueSummaryQueryOptions(userId, q, activeGame)
  );

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
        <h2 className="mb-2 text-lg font-medium">{activeGame} Breakdown</h2>
        {data.byGame.length === 0 ? (
          <p className="text-sm text-gray-500">No game breakdown available.</p>
        ) : (
          <ul className="space-y-1 text-sm text-gray-700">
            {data.byGame.map((slice) => (
              <li key={slice.game} className="flex justify-between">
                <span>{slice.game}</span>
                <span>
                  {(slice.cents / 100).toLocaleString(undefined, {
                    style: "currency",
                    currency: "USD",
                  })}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
