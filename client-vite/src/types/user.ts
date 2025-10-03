export type ApiUser = {
  id: number;
  name?: string | null;
  displayName?: string | null;
  username?: string | null;
  isAdmin?: boolean | null;
};

export type UserLite = {
  id: number;
  name: string;
  isAdmin: boolean;
};
