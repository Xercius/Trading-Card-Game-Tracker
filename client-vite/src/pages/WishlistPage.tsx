import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { useUser } from "@/context/useUser";
import http from "@/lib/http";

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
  const [searchParams] = useSearchParams();
  const q = searchParams.get("q") ?? "";
  const game = searchParams.get("game") ?? "";
  const [page, setPage] = useState(1);
  const pageSize = 50;
  const { data, isLoading, error } = useQuery<Paged<WishlistItemDto>>({
    queryKey: ["wishlist", userId, page, pageSize, q, game],
    queryFn: async () => {
      if (!userId) throw new Error("User not selected");
      const res = await http.get<Paged<WishlistItemDto>>(`user/${userId}/wishlist`, {
        params: { page, pageSize, q, game },
      });
      return res.data;
    },
    enabled: !!userId,
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
        {items.map(i => (
          <li key={i.cardPrintingId}>
            {i.game} — {i.cardName} · want {i.quantityWanted}
          </li>
        ))}
      </ul>
    </div>
  );
}