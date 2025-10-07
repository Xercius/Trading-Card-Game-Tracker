import { useCallback, useEffect, useState } from "react";
import { useQueryState } from "./useQueryState";
import { LS_INCLUDE_PROXIES_KEY } from "@/constants";

function parseBoolean(value: string | null | undefined): boolean | null {
  if (!value) return null;
  if (value === "1" || value.toLowerCase() === "true") return true;
  if (value === "0" || value.toLowerCase() === "false") return false;
  return null;
}

function readStoredIncludeProxies(): boolean {
  if (typeof window === "undefined") return false;
  const stored = window.localStorage.getItem(LS_INCLUDE_PROXIES_KEY);
  return parseBoolean(stored) ?? false;
}

export function useIncludeProxies(): [boolean, (next: boolean) => void] {
  const [queryValue, setQueryValue] = useQueryState("includeProxies", "");
  const [includeProxies, setIncludeProxies] = useState(() => {
    const fromQuery = parseBoolean(queryValue);
    if (fromQuery != null) return fromQuery;
    return readStoredIncludeProxies();
  });

  useEffect(() => {
    const parsed = parseBoolean(queryValue);
    if (parsed == null) return;
    setIncludeProxies(parsed);
    if (typeof window !== "undefined") {
      window.localStorage.setItem(LS_INCLUDE_PROXIES_KEY, parsed ? "1" : "0");
    }
  }, [queryValue]);

  const update = useCallback((next: boolean) => {
    setIncludeProxies(next);
    if (typeof window !== "undefined") {
      window.localStorage.setItem(LS_INCLUDE_PROXIES_KEY, next ? "1" : "0");
    }
    setQueryValue(next ? "1" : "");
  }, [setQueryValue]);

  return [includeProxies, update];
}
