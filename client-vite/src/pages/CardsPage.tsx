import { useQuery } from "@tanstack/react-query";
import { useUser } from "@/context/useUser";
import http from "@/lib/http";
import { useListQuery } from "@/hooks/useListQuery";

type CardDto = { cardId: number; game: string; name: string };
type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export default function CardsPage() {
  const { userId } = useUser();
  const { q, gameCsv } = useListQuery();
  const { data, isLoading, error } = useQuery<Paged<CardDto>>({
    queryKey: ["cards", userId, q, gameCsv],
    queryFn: async () => {
      const res = await http.get<Paged<CardDto>>("card", { params: { q, game: gameCsv } });
      return res.data;
    },
    enabled: !!userId,
  });

  const items = data?.items ?? [];

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading cards</div>;
  if (items.length === 0) return <div className="p-4">No cards found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">
        Showing {items.length} of {data?.total ?? 0}
      </div>
      <ul className="list-disc pl-6">
        {items.map(c => (
          <li key={c.cardId}>{c.game} — {c.name}</li>
        ))}
      </ul>
    </div>
  );
}