import { createContext, useContext, useMemo, useState } from 'react';
import { setApiUserId } from '@/lib/api';

type Ctx = { userId: number; setUserId: (id: number) => void };
const UserCtx = createContext<Ctx | undefined>(undefined);

export function UserProvider({ children }: { children: React.ReactNode }) {
  const [userId, setUserIdState] = useState<number>(() => {
    const saved = localStorage.getItem('userId');
    return saved ? Number(saved) : 1;
  });

  setApiUserId(userId);

  const setUserId = (id: number) => {
    localStorage.setItem('userId', String(id));
    setUserIdState(id);
    setApiUserId(id);
  };

  const value = useMemo(() => ({ userId, setUserId }), [userId]);
  return <UserCtx.Provider value={value}>{children}</UserCtx.Provider>;
}

export const useUser = () => {
  const ctx = useContext(UserCtx);
  if (!ctx) throw new Error('useUser must be used within UserProvider');
  return ctx;
};
