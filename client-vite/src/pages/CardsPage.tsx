import { useQuery } from '@tanstack/react-query';
import { useUser } from '@/context/UserProvider';
import { api } from '@/lib/api';

type CardDto = { id: number; game: string; name: string };

export default function CardsPage() {
  const { userId } = useUser();
  const { data, isLoading, error } = useQuery({
    queryKey: ['cards', userId],
    queryFn: async () => {
      const res = await api.get<CardDto[]>('/card');
      return res.data;
    },
  });

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading cards</div>;

  return (
    <ul className="p-4 list-disc">
      {data?.map((c) => (
        <li key={c.id}>{c.game} — {c.name}</li>
      ))}
    </ul>
  );
}
