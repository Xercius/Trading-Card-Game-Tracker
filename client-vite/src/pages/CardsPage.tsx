import { useQuery } from '@tanstack/react-query';
import { useUser } from '@/context/useUser';
import { api } from '@/lib/api';

type CardDto = { cardid: number; game: string; name: string };
type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number
};

export default function CardsPage() {
  const { userId } = useUser();
  const { data, isLoading, error } = useQuery<Paged<CardDto>>({
    queryKey: ['cards', userId],
    queryFn: async () => {
      const res = await api.get<Paged<CardDto>>('/card');
      return res.data;
    },
  });

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading cards</div>;
  if (!data || data.items.length === 0) return <div className="p-4">No cards found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">
        Showing {data.items.length} of {data.total}
      </div>
      <ul className="list-disc pl-6">
        {data.items.map(c => (
          <li key={c.cardid}>{c.game} — {c.name}</li>
        ))}
      </ul>
    </div>
  );
}