import { useQuery } from '@tanstack/react-query';
import { useUser } from '@/context/UserProvider';
import { api } from '@/lib/api';

type DeckDto = { id: number; game: string; name: string; description?: string };
type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export default function DecksPage() {
  const { userId } = useUser();
  const { data, isLoading, error } = useQuery<Paged<DeckDto>>({
    queryKey: ['decks', userId],
    queryFn: async () => {
      const res = await api.get<Paged<DeckDto>>(`/user/${userId}/deck`);
      return res.data;
    },
  });

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading decks</div>;
  if (!data || data.items.length === 0) return <div className="p-4">No decks found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">
        Showing {data.items.length} of {data.total}
      </div>
      <ul className="list-disc pl-6">
        {data.items.map(deck => (
          <li key={deck.id}>
            {deck.game} — {deck.name}
            {deck.description ? ` (${deck.description})` : ''}
          </li>
        ))}
      </ul>
    </div>
  );
}

