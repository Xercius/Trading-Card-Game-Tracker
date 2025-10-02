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
export default function WishlistPage() {
  const { userId } = useUser();
  const { data: items = [], isLoading, error } = useQuery<WishlistItemDto[]>({
    queryKey: ['wishlist', userId],
    queryFn: async () => {
      const res = await api.get<WishlistItemDto[]>(`/user/${userId}/wishlist`);
      return res.data;
    },
  });

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading wishlist</div>;
  if (items.length === 0) return <div className="p-4">No wishlist items found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">Showing {items.length} items</div>
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