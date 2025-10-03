import { useQuery } from "@tanstack/react-query";
import http from "@/lib/http";
import { mapUser } from "@/lib/mapUser";
import type { ApiUser, UserLite } from "@/types/user";

type Paged<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export default function UsersPage() {
  const { data, isLoading, isError } = useQuery<Paged<UserLite>>({
    queryKey: ["users"],
    queryFn: async () => {
      const res = await http.get<ApiUser[]>("user");
      const users: UserLite[] = res.data.map(mapUser);
      return { items: users, total: users.length, page: 1, pageSize: users.length };
    },
  });

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
