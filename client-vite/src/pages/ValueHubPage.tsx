import { useQuery } from '@tanstack/react-query';
import { useUser } from '@/context/UserProvider';
import { api } from '@/lib/api';

type ValuePointDto = { asOfUtc: string; priceCents: number };
type ValueSummaryDto = { latestCents: number; points: ValuePointDto[] };

export default function ValueHubPage() {
  const { userId } = useUser();
  const { data, isLoading, error } = useQuery<ValueSummaryDto>({
    queryKey: ['value', userId],
    queryFn: async () => {
      const res = await api.get<ValueSummaryDto>(
        `/api/value/collection/summary?userId=${userId}`,
      );
      return res.data;
    },
  });

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading value summary</div>;
  if (!data) return <div className="p-4">No value data found</div>;

  const formattedTotal = (data.latestCents / 100).toLocaleString(undefined, {
    style: 'currency',
    currency: 'USD',
  });

  return (
    <div className="p-4">
      <h1 className="mb-2 text-xl font-semibold">Collection Value</h1>
      <p>Latest total: {formattedTotal}</p>
      <div className="mt-4 text-sm text-gray-500">History points: {data.points.length}</div>
    </div>
  );
}

