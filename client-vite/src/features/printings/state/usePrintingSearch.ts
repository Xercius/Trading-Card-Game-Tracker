import * as React from "react";
import { useDebouncedCallback } from "use-debounce";
import { useSearchParams } from "react-router-dom";
import type { PrintingQuery } from "../api/printings";

const parseCsv = (value: string | null): string[] =>
  value ? value.split(",").map((item) => item.trim()).filter(Boolean) : [];

const toCsv = (arr: string[]): string | null => (arr.length > 0 ? arr.join(",") : null);

const parseNumber = (value: string | null, fallback: number): number => {
  if (!value) return fallback;
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
};

const normalizeQuery = (query: PrintingQuery): PrintingQuery => ({
  q: query.q ?? "",
  game: query.game ? query.game.filter(Boolean) : [],
  set: query.set ? query.set.filter(Boolean) : [],
  rarity: query.rarity ? query.rarity.filter(Boolean) : [],
  page: query.page && Number.isFinite(query.page) && query.page > 0 ? query.page : 1,
  pageSize: query.pageSize && Number.isFinite(query.pageSize) && query.pageSize > 0 ? query.pageSize : 60,
});

const parseSearchParams = (params: URLSearchParams): PrintingQuery =>
  normalizeQuery({
    q: params.get("q") ?? "",
    game: parseCsv(params.get("game")),
    set: parseCsv(params.get("set")),
    rarity: parseCsv(params.get("rarity")),
    page: parseNumber(params.get("page"), 1),
    pageSize: parseNumber(params.get("pageSize"), 60),
  });

const arraysEqual = (a: string[], b: string[]): boolean =>
  a.length === b.length && a.every((value, index) => value === b[index]);

const queriesEqual = (a: PrintingQuery, b: PrintingQuery): boolean =>
  a.q === b.q &&
  arraysEqual(a.game ?? [], b.game ?? []) &&
  arraysEqual(a.set ?? [], b.set ?? []) &&
  arraysEqual(a.rarity ?? [], b.rarity ?? []) &&
  (a.page ?? 1) === (b.page ?? 1) &&
  (a.pageSize ?? 60) === (b.pageSize ?? 60);

export function usePrintingSearch(): [
  PrintingQuery,
  (updater: (query: PrintingQuery) => PrintingQuery) => void,
] {
  const [searchParams, setSearchParams] = useSearchParams();
  const [query, setQueryState] = React.useState<PrintingQuery>(() => parseSearchParams(searchParams));

  React.useEffect(() => {
    const next = parseSearchParams(searchParams);
    setQueryState((prev) => (queriesEqual(prev, next) ? prev : next));
  }, [searchParams]);

  const updateSearchParams = useDebouncedCallback((nextQuery: PrintingQuery) => {
    const next = new URLSearchParams();
    if (nextQuery.q) next.set("q", nextQuery.q);
    const gameCsv = toCsv(nextQuery.game ?? []);
    if (gameCsv) next.set("game", gameCsv);
    const setCsv = toCsv(nextQuery.set ?? []);
    if (setCsv) next.set("set", setCsv);
    const rarityCsv = toCsv(nextQuery.rarity ?? []);
    if (rarityCsv) next.set("rarity", rarityCsv);
    next.set("page", String(nextQuery.page ?? 1));
    next.set("pageSize", String(nextQuery.pageSize ?? 60));
    setSearchParams(next, { replace: true });
  }, 100);

  React.useEffect(() => {
    updateSearchParams(query);
    return () => {
      updateSearchParams.cancel();
    };
  }, [query, updateSearchParams]);

  const setQuery = React.useCallback(
    (updater: (current: PrintingQuery) => PrintingQuery) => {
      setQueryState((prev) => normalizeQuery(updater(prev)));
    },
    []
  );

  return [query, setQuery];
}
