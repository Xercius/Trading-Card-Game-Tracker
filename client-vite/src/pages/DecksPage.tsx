import { useEffect, useRef } from "react";
import { useQuery } from "@tanstack/react-query";
import { useUser } from "@/context/useUser";
import http from "@/lib/http";
import { useQueryState } from "@/hooks/useQueryState";

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
  const [q] = useQueryState("q", "");
  const [gameCsv] = useQueryState("game", "");
  const [pageParamRaw, setPageParam] = useQueryState("page", "1");
  const [pageSizeParamRaw] = useQueryState("pageSize", String(DEFAULT_PAGE_SIZE));

  const parsedPage = Number(pageParamRaw);
  const parsedPageSize = Number(pageSizeParamRaw);
  const page = Number.isFinite(parsedPage) && parsedPage > 0 ? Math.floor(parsedPage) : 1;
  const pageSize =
    Number.isFinite(parsedPageSize) && parsedPageSize > 0
      ? Math.floor(parsedPageSize)
      : DEFAULT_PAGE_SIZE;

  const previousFiltersRef = useRef({ q, gameCsv });
  const shouldResetPage =
    pageParamRaw !== "1" &&
    (previousFiltersRef.current.q !== q || previousFiltersRef.current.gameCsv !== gameCsv);

  useEffect(() => {
    if (shouldResetPage) {
      setPageParam("1");
    }
    previousFiltersRef.current = { q, gameCsv };
  }, [shouldResetPage, setPageParam, q, gameCsv]);

  const updatePage = (nextPage: number) => {
    const safeNext = nextPage < 1 ? 1 : nextPage;
    setPageParam(String(safeNext));
  };
  const { data, isLoading, error } = useQuery<Paged<DeckDto>>({
    queryKey: ["decks", userId, page, pageSize, q, gameCsv],
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