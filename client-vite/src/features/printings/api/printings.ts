export type PrintingListItem = {
  printingId: string;
  cardId: string;
  cardName: string;
  game: string;
  setName: string;
  setCode?: string | null;
  number?: string | null;
  rarity?: string | null;
  imageUrl?: string | null;
};

export type PrintingQuery = {
  q?: string;
  game?: string[];
  set?: string[];
  rarity?: string[];
  page?: number;
  pageSize?: number;
};

const toQueryString = (q: PrintingQuery) => {
  const p = new URLSearchParams();
  if (q.q) p.set("q", q.q);
  if (q.game?.length) p.set("game", q.game.join(","));
  if (q.set?.length) p.set("set", q.set.join(","));
  if (q.rarity?.length) p.set("rarity", q.rarity.join(","));
  if (q.page) p.set("page", String(q.page));
  if (q.pageSize) p.set("pageSize", String(q.pageSize));
  return p.toString();
};

export async function fetchPrintings(query: PrintingQuery): Promise<PrintingListItem[]> {
  const base = import.meta.env.VITE_API_BASE ?? "/api";
  const qs = toQueryString(query);
  const res = await fetch(`${base}/cards/printings${qs ? `?${qs}` : ""}`);
  if (!res.ok) throw new Error(`Failed to load printings: ${res.status}`);
  return res.json();
}
