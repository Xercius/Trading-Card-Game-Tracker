import { useMemo, useState, useCallback } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { setApiUserId } from '@/lib/api';
import { UserCtx } from './UserCtx';

export function UserProvider({ children }: { children: React.ReactNode }) {
  const qc = useQueryClient();

  const [userId, setUserIdState] = useState<number>(() => {
    const saved = localStorage.getItem('userId');
    return saved ? Number(saved) : 1;
  });

  setApiUserId(userId);

  const setUserId = useCallback((id: number) => {
    localStorage.setItem('userId', String(id));
    setUserIdState(id);
    setApiUserId(id);
    qc.invalidateQueries();
  }, [qc]);

  const value = useMemo(() => ({ userId, setUserId }), [userId, setUserId]);

  return <UserCtx.Provider value={value}>{children}</UserCtx.Provider>;
}