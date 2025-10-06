import { useQuery } from "@tanstack/react-query";
import http from "@/lib/http";
import { mapUser } from "@/lib/mapUser";
import type { ApiUser, UserLite } from "@/types/user";
import { useUser } from "@/state/useUser";

type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export default function UsersPage() {
  const { users, userId } = useUser();
  const currentUser = users.find((u) => u.id === userId);
  const enabled = currentUser?.isAdmin ?? false;

  const { data, isLoading, isError } = useQuery<Paged<UserLite>>({
    queryKey: ["adminUsers"],
    queryFn: async () => {
      const res = await http.get<ApiUser[]>("user");
      const mapped: UserLite[] = res.data.map(mapUser);
      return { items: mapped, total: mapped.length, page: 1, pageSize: mapped.length };
    },
    enabled,
  });

  if (!enabled) return <div className="p-4">Admins only.</div>;
  if (isLoading) return <div className="p-4">Loading…</div>;
  if (isError) return <div className="p-4 text-red-500">Error loading users</div>;
  if (!data || data.items.length === 0) return <div className="p-4">No users found</div>;

  return (
    <div className="p-4">
      <div className="mb-2 text-sm text-gray-500">
        Showing {data.items.length} of {data.total}
      </div>
      <ul className="list-disc pl-6">
        {data.items.map(user => (
          <li key={user.id}>
            {user.name} — {user.isAdmin ? "Admin" : "User"}
          </li>
        ))}
      </ul>
    </div>
  );
}
