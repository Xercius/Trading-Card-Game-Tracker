import type { ApiUser, UserLite } from "@/types/user";

export function mapUser(u: ApiUser): UserLite {
  const name =
    (u.name && u.name.trim()) ||
    (u.displayName && u.displayName.trim()) ||
    (u.username && u.username.trim()) ||
    `User ${u.id}`;

  return {
    id: u.id,
    name,
    isAdmin: Boolean(u.isAdmin),
  };
}
