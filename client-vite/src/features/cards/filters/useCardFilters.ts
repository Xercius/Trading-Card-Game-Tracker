import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router-dom";

export type CardFilters = {
  q: string;
  games: string[];
  sets: string[];
  rarities: string[];
  types: string[];
  arenas: string[];
  aspects: string[];
  traits: string[];
  page: number;
  pageSize: number;
  sort?: string;
};

const filterKeys = [
  "game",
  "set",
  "rarity",
  "type",
  "arena",
  "aspect",
  "trait",
  "q",
  "page",
  "pageSize",
  "sort",
] as const;

type SetFiltersFn = (updater: CardFilters | ((prev: CardFilters) => CardFilters)) => void;

type UseCardFiltersResult = {
  filters: CardFilters;
  setFilters: SetFiltersFn;
  clearAll: () => void;
  toQueryKey: () => readonly [
    string,
    string,
    string,
    string,
    string,
    string,
    string,
    string,
    number,
    number,
    string,
  ];
};

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 60;

const emptyFilters: CardFilters = {
  q: "",
  games: [],
  sets: [],
  rarities: [],
  types: [],
  arenas: [],
  aspects: [],
  traits: [],
  page: DEFAULT_PAGE,
  pageSize: DEFAULT_PAGE_SIZE,
  sort: undefined,
};

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
    games: sanitizeList(input.games ?? []),
    sets: sanitizeList(input.sets ?? []),
    rarities: sanitizeList(input.rarities ?? []),
    types: sanitizeList(input.types ?? []),
    arenas: sanitizeList(input.arenas ?? []),
    aspects: sanitizeList((input.aspects ?? []).filter((a) => a.trim().toLowerCase() !== "all")),
    traits: sanitizeList(input.traits ?? []),
    page: input.page > 0 ? Math.floor(input.page) : DEFAULT_PAGE,
    pageSize: input.pageSize > 0 ? Math.floor(input.pageSize) : DEFAULT_PAGE_SIZE,
    sort: input.sort?.trim() || undefined,
  };
}

function parseFilters(params: URLSearchParams): CardFilters {
  if (!params) return emptyFilters;

  const q = params.get("q") ?? "";
  const games = parseCsv(params.get("game"));
  const sets = parseCsv(params.get("set"));
  const rarities = parseCsv(params.get("rarity"));
  const types = parseCsv(params.get("type"));
  const arenas = parseCsv(params.get("arena"));
  const aspects = parseCsv(params.get("aspect"));
  const traits = parseCsv(params.get("trait"));

  const pageStr = params.get("page");
  const page = pageStr ? Math.max(1, parseInt(pageStr, 10) || DEFAULT_PAGE) : DEFAULT_PAGE;

  const pageSizeStr = params.get("pageSize");
  const pageSize = pageSizeStr
    ? Math.max(1, parseInt(pageSizeStr, 10) || DEFAULT_PAGE_SIZE)
    : DEFAULT_PAGE_SIZE;

  const sort = params.get("sort") || undefined;

  return sanitizeFilters({
    q,
    games,
    sets,
    rarities,
    types,
    arenas,
    aspects,
    traits,
    page,
    pageSize,
    sort,
  });
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
  if (filters.types.length > 0) {
    next.set("type", filters.types.join(","));
  }
  if (filters.arenas.length > 0) {
    next.set("arena", filters.arenas.join(","));
  }
  if (filters.aspects.length > 0) {
    next.set("aspect", filters.aspects.join(","));
  }
  if (filters.traits.length > 0) {
    next.set("trait", filters.traits.join(","));
  }
  if (filters.q.length > 0) {
    next.set("q", filters.q);
  }

  // Always include page and pageSize
  next.set("page", String(filters.page));
  next.set("pageSize", String(filters.pageSize));

  if (filters.sort) {
    next.set("sort", filters.sort);
  }

  return next;
}

/**
 * Custom hook to read and update card search filters synchronized with URL query parameters.
 */
export function useCardFilters(): UseCardFiltersResult {
  const [searchParams, setSearchParams] = useSearchParams();
  const serialized = searchParams.toString();

  const filters = useMemo(() => parseFilters(new URLSearchParams(serialized)), [serialized]);

  const gamesKey = filters.games.join("|");
  const setsKey = filters.sets.join("|");
  const raritiesKey = filters.rarities.join("|");
  const typesKey = filters.types.join("|");
  const arenasKey = filters.arenas.join("|");
  const aspectsKey = filters.aspects.join("|");
  const traitsKey = filters.traits.join("|");

  const stableKey = useMemo(
    () =>
      [
        filters.q,
        gamesKey,
        setsKey,
        raritiesKey,
        typesKey,
        arenasKey,
        aspectsKey,
        traitsKey,
        filters.page,
        filters.pageSize,
        filters.sort ?? "",
      ] as const,
    [
      filters.q,
      gamesKey,
      setsKey,
      raritiesKey,
      typesKey,
      arenasKey,
      aspectsKey,
      traitsKey,
      filters.page,
      filters.pageSize,
      filters.sort,
    ]
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
