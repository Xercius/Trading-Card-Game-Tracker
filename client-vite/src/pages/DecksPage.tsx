import { useEffect, useRef } from "react";
import { useQuery } from "@tanstack/react-query";
import { useUser } from "@/context/useUser";
import http from "@/lib/http";
import { useListQuery } from "@/hooks/useListQuery";

const DEFAULT_PAGE_SIZE = 50;

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
  const { q, gameCsv, params, setSearchParams } = useListQuery();
  const pageParam = Number(params.get("page") ?? "1");
  const pageSizeParam = Number(params.get("pageSize") ?? String(DEFAULT_PAGE_SIZE));
  const page = Number.isFinite(pageParam) && pageParam > 0 ? Math.floor(pageParam) : 1;
  const pageSize =
    Number.isFinite(pageSizeParam) && pageSizeParam > 0
      ? Math.floor(pageSizeParam)
      : DEFAULT_PAGE_SIZE;

  const previousFiltersRef = useRef({ q, gameCsv });
  const shouldResetPage =
    params.has("page") &&
    (params.get("page") ?? "1") !== "1" &&
    (previousFiltersRef.current.q !== q || previousFiltersRef.current.gameCsv !== gameCsv);

  useEffect(() => {
    if (shouldResetPage) {
      const nextParams = new URLSearchParams(params);
      nextParams.set("page", "1");
      nextParams.set("pageSize", String(pageSize));
      setSearchParams(nextParams, { replace: true });
    }
    previousFiltersRef.current = { q, gameCsv };
  }, [shouldResetPage, params, pageSize, setSearchParams, q, gameCsv]);

  const updatePage = (nextPage: number) => {
    const safeNext = nextPage < 1 ? 1 : nextPage;
    const nextParams = new URLSearchParams(params);
    nextParams.set("page", String(safeNext));
    nextParams.set("pageSize", String(pageSize));
    setSearchParams(nextParams);
  };
  const { data, isLoading, error } = useQuery<Paged<DeckDto>>({
    queryKey: ["decks", userId, q, gameCsv, page, pageSize],
    queryFn: async () => {
      if (!userId) throw new Error("User not selected");
      const res = await http.get<Paged<DeckDto>>(`user/${userId}/deck`, {
        params: { page, pageSize, q, game: gameCsv },
      });
      return res.data;
    },
    enabled: !!userId && !shouldResetPage,
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
            onClick={() => canGoPrev && updatePage(page - 1)}
            disabled={!canGoPrev}
            type="button"
          >
            Prev
          </button>
          <button
            className="rounded border px-2 py-1 disabled:opacity-50"
            onClick={() => canGoNext && updatePage(page + 1)}
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