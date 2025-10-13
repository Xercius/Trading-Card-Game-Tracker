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

export async function fetchPrintings(query: PrintingQuery): Promise<PrintingListItem[]> {
  const base = import.meta.env.VITE_API_BASE ?? "/api";
  const params = new URLSearchParams();
  if (query.q) params.set("q", query.q);
  if (query.game?.length) params.set("game", query.game.join(","));
  if (query.set?.length) params.set("set", query.set.join(","));
  if (query.rarity?.length) params.set("rarity", query.rarity.join(","));
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));

  const paramString = params.toString();
  const res = await fetch(`${base}/cards/printings${paramString ? `?${paramString}` : ""}`);
  if (!res.ok) {
    throw new Error(`Failed to fetch printings. Status: ${res.status} ${res.statusText}`);
  }
  return (await res.json()) as PrintingListItem[];
}
