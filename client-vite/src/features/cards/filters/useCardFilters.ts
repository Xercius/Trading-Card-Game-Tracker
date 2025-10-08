import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router-dom";

export type CardFilters = {
  q: string;
  games: string[];
  sets: string[];
  rarities: string[];
};

const filterKeys = ["game", "set", "rarity", "q"] as const;

type SetFiltersFn = (updater: CardFilters | ((prev: CardFilters) => CardFilters)) => void;

type UseCardFiltersResult = {
  filters: CardFilters;
  setFilters: SetFiltersFn;
  clearAll: () => void;
  toQueryKey: () => readonly [string, string, string, string];
};

const emptyFilters: CardFilters = { q: "", games: [], sets: [], rarities: [] };

function parseCsv(value: string | null): string[] {
  if (!value) return [];
  return value
    .split(",")
    .map((part) => part.trim())
    .filter(Boolean);
}

function sanitizeList(values: string[]): string[] {
  const seen = new Set<string>();
  const result: string[] = [];
  for (const value of values) {
    const trimmed = value.trim();
    if (!trimmed || seen.has(trimmed)) continue;
    seen.add(trimmed);
    result.push(trimmed);
  }
  return result;
}

function sanitizeFilters(input: CardFilters): CardFilters {
  return {
    q: input.q.trim(),
    games: sanitizeList(input.games),
    sets: sanitizeList(input.sets),
    rarities: sanitizeList(input.rarities),
  };
}

function parseFilters(params: URLSearchParams): CardFilters {
  if (!params) return emptyFilters;

  const q = params.get("q") ?? "";
  const games = parseCsv(params.get("game"));
  const sets = parseCsv(params.get("set"));
  const rarities = parseCsv(params.get("rarity"));

  return sanitizeFilters({ q, games, sets, rarities });
}

function applyFiltersToParams(prev: URLSearchParams, filters: CardFilters): URLSearchParams {
  const next = new URLSearchParams(prev);
  for (const key of filterKeys) {
    next.delete(key);
  }

  if (filters.games.length > 0) {
    next.set("game", filters.games.join(","));
  }
  if (filters.sets.length > 0) {
    next.set("set", filters.sets.join(","));
  }
  if (filters.rarities.length > 0) {
    next.set("rarity", filters.rarities.join(","));
  }
  if (filters.q.length > 0) {
    next.set("q", filters.q);
  }

  return next;
}

export function useCardFilters(): UseCardFiltersResult {
  const [searchParams, setSearchParams] = useSearchParams();
  const serialized = searchParams.toString();

  const filters = useMemo(() => parseFilters(searchParams), [serialized]);

  const gamesKey = filters.games.join("|");
  const setsKey = filters.sets.join("|");
  const raritiesKey = filters.rarities.join("|");

  const stableKey = useMemo(
    () => [filters.q, gamesKey, setsKey, raritiesKey] as const,
    [filters.q, gamesKey, setsKey, raritiesKey]
  );

  const setFilters = useCallback<SetFiltersFn>(
    (updater) => {
      setSearchParams(
        (prev) => {
          const prevFilters = parseFilters(prev);
          const nextFilters = sanitizeFilters(
            typeof updater === "function" ? updater(prevFilters) : updater
          );

          return applyFiltersToParams(prev, nextFilters);
        },
        { replace: true }
      );
    },
    [setSearchParams]
  );

  const clearAll = useCallback(() => {
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        for (const key of filterKeys) {
          next.delete(key);
        }
        return next;
      },
      { replace: true }
    );
  }, [setSearchParams]);

  const toQueryKey = useCallback(() => stableKey, [stableKey]);

  return {
    filters,
    setFilters,
    clearAll,
    toQueryKey,
  };
}
