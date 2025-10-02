import { useQuery } from '@tanstack/react-query';
import { useUser } from '@/context/useUser';
import { api } from '@/lib/api';

type DeckDto = {
  id: number;
  userId: number;
  game: string;
  name: string;
  description: string | null;
  createdUtc: string;
  updatedUtc: string | null;
};
export default function DecksPage() {
  const { userId } = useUser();
  const { data: decks = [], isLoading, error } = useQuery<DeckDto[]>({
    queryKey: ['decks', userId],
    queryFn: async () => {
      const res = await api.get<DeckDto[]>(`/user/${userId}/deck`);
      return res.data;
    },
  });

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading decks</div>;
  if (decks.length === 0) return <div className="p-4">No decks found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">Showing {decks.length} decks</div>
      <ul className="list-disc pl-6">
        {decks.map(d => (
          <li key={d.id}>
            {d.game} — {d.name}{d.description ? ` (${d.description})` : ''} · {new Date(d.createdUtc).toLocaleDateString()}
          </li>
        ))}
      </ul>
    </div>
  );
}