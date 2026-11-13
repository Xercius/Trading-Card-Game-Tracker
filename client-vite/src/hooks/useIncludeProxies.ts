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
  try {
    return parseBoolean(window.localStorage.getItem(LS_INCLUDE_PROXIES_KEY)) ?? false;
  } catch {
    return false;
  }
}

function writeStoredIncludeProxies(value: boolean): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(LS_INCLUDE_PROXIES_KEY, value ? "1" : "0");
  } catch {
    // Ignore storage quota / privacy mode errors
  }
}

/**
 * Persists the "include proxies" toggle in both the URL query string and localStorage.
 */
export function useIncludeProxies(): [boolean, (next: boolean) => void] {
  const [queryValue, setQueryValue] = useQueryState("includeProxies", "");
  const parsedQuery = parseBoolean(queryValue);
  const [storedValue, setStoredValue] = useState<boolean>(() =>
    parsedQuery !== null ? parsedQuery : readStoredIncludeProxies()
  );

  const includeProxies = parsedQuery ?? storedValue;

  useEffect(() => {
    if (parsedQuery !== null) {
      writeStoredIncludeProxies(parsedQuery);
    }
  }, [parsedQuery]);

  const update = useCallback(
    (next: boolean) => {
      setStoredValue(next);
      writeStoredIncludeProxies(next);
      setQueryValue(next ? "1" : "");
    },
    [setQueryValue]
  );

  return [includeProxies, update];
}
