import { useQuery } from '@tanstack/react-query';
import { useUser } from '@/context/UserProvider';
import { api } from '@/lib/api';

type WishlistItemDto = { id: number; game: string; name: string; desired: number };
type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export default function WishlistPage() {
  const { userId } = useUser();
  const { data, isLoading, error } = useQuery<Paged<WishlistItemDto>>({
    queryKey: ['wishlist', userId],
    queryFn: async () => {
      const res = await api.get<Paged<WishlistItemDto>>(`/user/${userId}/wishlist`);
      return res.data;
    },
  });

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading wishlist</div>;
  if (!data || data.items.length === 0) return <div className="p-4">No wishlist items found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">
        Showing {data.items.length} of {data.total}
      </div>
      <ul className="list-disc pl-6">
        {data.items.map(item => (
          <li key={item.id}>
            {item.game} — {item.name} (want {item.desired})
          </li>
        ))}
      </ul>
    </div>
  );
}

