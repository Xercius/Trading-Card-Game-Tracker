import { useCallback } from "react";
import { useSearchParams } from "react-router-dom";

export function useQueryState(key: string, initial = ""): [string, (v: string) => void] {
  const [params, setParams] = useSearchParams();
  const val = params.get(key) ?? initial;
  const setVal = useCallback(
    (v: string) => {
      const next = new URLSearchParams(params);
      if (v) next.set(key, v);
      else next.delete(key);
      setParams(next, { replace: true });
    },
    [key, params, setParams]
  );
  return [val, setVal];
}
