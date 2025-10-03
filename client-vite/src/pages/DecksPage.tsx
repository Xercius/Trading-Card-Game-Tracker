import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { useUser } from "@/context/useUser";
import http from "@/lib/http";

type DeckDto = {
  id: number;
  userId: number;
  game: string;
  name: string;
  description: string | null;
  createdUtc: string;
  updatedUtc: string | null;
};

type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export default function DecksPage() {
  const { userId } = useUser();
  const [searchParams] = useSearchParams();
  const q = searchParams.get("q") ?? "";
  const game = searchParams.get("game") ?? "";
  const [page, setPage] = useState(1);
  const pageSize = 50;
  const { data, isLoading, error } = useQuery<Paged<DeckDto>>({
    queryKey: ["decks", userId, page, pageSize, q, game],
    queryFn: async () => {
      if (!userId) throw new Error("User not selected");
      const res = await http.get<Paged<DeckDto>>(`user/${userId}/deck`, {
        params: { page, pageSize, q, game },
      });
      return res.data;
    },
    enabled: !!userId,
  });

  const decks = data?.items ?? [];
  const total = data?.total ?? 0;
  const canGoPrev = page > 1;
  const canGoNext = decks.length === pageSize && page * pageSize < total;

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading decks</div>;
  if (decks.length === 0) return <div className="p-4">No decks found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">
        Showing {decks.length} of {total} decks
        <div className="mt-2 flex gap-2">
          <button
            className="rounded border px-2 py-1 disabled:opacity-50"
            onClick={() => canGoPrev && setPage(p => Math.max(1, p - 1))}
            disabled={!canGoPrev}
            type="button"
          >
            Prev
          </button>
          <button
            className="rounded border px-2 py-1 disabled:opacity-50"
            onClick={() => canGoNext && setPage(p => p + 1)}
            disabled={!canGoNext}
            type="button"
          >
            Next
          </button>
        </div>
      </div>
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