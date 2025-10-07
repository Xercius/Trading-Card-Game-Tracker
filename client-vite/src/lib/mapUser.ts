import type { AdminUser, AdminUserApi, ApiUser, UserLite } from "@/types/user";

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

export function mapAdminUser(u: AdminUserApi): AdminUser {
  const lite = mapUser(u);
  const username = (u.username && u.username.trim()) || lite.name;
  const displayName = (u.displayName && u.displayName.trim()) || lite.name;
  const createdUtc = u.createdUtc ?? new Date().toISOString();

  return {
    ...lite,
    username,
    displayName,
    createdUtc,
  };
}
