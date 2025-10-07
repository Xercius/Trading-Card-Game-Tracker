export type ApiUser = {
  id: number;
  name?: string | null;
  displayName?: string | null;
  username?: string | null;
  isAdmin?: boolean | null;
  createdUtc?: string | null;
};

export type UserLite = {
  id: number;
  name: string;
  isAdmin: boolean;
};

export type AdminUserApi = ApiUser & {
  createdUtc: string;
  isAdmin: boolean;
};

export type AdminUser = UserLite & {
  username: string;
  displayName: string;
  createdUtc: string;
};
