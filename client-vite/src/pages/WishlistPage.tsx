import { useEffect, useRef } from "react";
import { useQuery } from "@tanstack/react-query";
import { useUser } from "@/state/useUser";
import http from "@/lib/http";
import { useQueryState } from "@/hooks/useQueryState";

const DEFAULT_PAGE_SIZE = 50;

type WishlistItemDto = {
  cardPrintingId: number;
  quantityWanted: number;
  cardId: number;
  cardName: string;
  game: string;
  set: string;
  number: string;
  rarity: string;
  style: string;
  imageUrl: string | null;
};

type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export default function WishlistPage() {
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
  const { data, isLoading, error } = useQuery<Paged<WishlistItemDto>>({
    queryKey: ["wishlist", userId, page, pageSize, q, gameCsv],
    queryFn: async () => {
      if (!userId) throw new Error("User not selected");
      const res = await http.get<Paged<WishlistItemDto>>(`user/${userId}/wishlist`, {
        params: { page, pageSize, name: q || undefined, game: gameCsv },
      });
      return res.data;
    },
    enabled: !!userId && !shouldResetPage,
  });

  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const canGoPrev = page > 1;
  const canGoNext = items.length === pageSize && page * pageSize < total;

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading wishlist</div>;
  if (items.length === 0) return <div className="p-4">No wishlist items found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">
        Showing {items.length} of {total} items
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
        {items.map(i => (
          <li key={i.cardPrintingId}>
            {i.game} — {i.cardName} · want {i.quantityWanted}
          </li>
        ))}
      </ul>
    </div>
  );
}