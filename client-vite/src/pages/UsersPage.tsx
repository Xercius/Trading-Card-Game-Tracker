import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

type UserDto = { id: number; name: string; role: string };
type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export default function UsersPage() {
  const { data, isLoading, error } = useQuery<Paged<UserDto>>({
    queryKey: ['users'],
    queryFn: async () => {
      const res = await api.get<Paged<UserDto>>('/user');
      return res.data;
    },
  });

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (error) return <div className="p-4 text-red-500">Error loading users</div>;
  if (!data || data.items.length === 0) return <div className="p-4">No users found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">
        Showing {data.items.length} of {data.total}
      </div>
      <ul className="list-disc pl-6">
        {data.items.map(user => (
          <li key={user.id}>
            {user.name} — {user.role}
          </li>
        ))}
      </ul>
    </div>
  );
}

