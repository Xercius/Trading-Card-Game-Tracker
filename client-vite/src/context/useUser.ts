import { useContext } from 'react';
import { UserCtx } from '@/context/UserCtx';

export function useUser() {
  const ctx = useContext(UserCtx);
  if (!ctx) throw new Error('useUser must be used within UserProvider');
  return ctx;
}
