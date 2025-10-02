import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useUser } from '@/context/useUser';
import { api } from '@/lib/api';

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
  const [page, setPage] = useState(1);
  const pageSize = 50;
  const { data, isLoading, error } = useQuery<Paged<WishlistItemDto>>({
    queryKey: ['wishlist', userId, page, pageSize],
    queryFn: async () => {
      const res = await api.get<Paged<WishlistItemDto>>(
        `/user/${userId}/wishlist?page=${page}&pageSize=${pageSize}`,
      );
      return res.data;
    },
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