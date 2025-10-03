import { useSearchParams } from "react-router-dom";

export function useListQuery() {
  const [params, setSearchParams] = useSearchParams();
  const q = params.get("q") ?? "";
  const gameCsv = params.get("game") ?? "";
  const games = gameCsv ? gameCsv.split(",").filter(Boolean) : [];
  return { q, gameCsv, games, params, setSearchParams };
}
