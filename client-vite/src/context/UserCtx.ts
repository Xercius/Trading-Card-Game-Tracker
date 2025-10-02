import { createContext } from 'react';

export type UserCtxType = { userId: number; setUserId: (id: number) => void };
export const UserCtx = createContext<UserCtxType | undefined>(undefined);
