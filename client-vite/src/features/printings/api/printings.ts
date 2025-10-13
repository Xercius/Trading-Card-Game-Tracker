import { api } from "@/lib/api";

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
  const params = new URLSearchParams();
  if (query.q) params.set("q", query.q);
  if (query.game?.length) params.set("game", query.game.join(","));
  if (query.set?.length) params.set("set", query.set.join(","));
  if (query.rarity?.length) params.set("rarity", query.rarity.join(","));
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));

  const res = await api.get<PrintingListItem[]>("cards/printings", { params: Object.fromEntries(params) });
  if (res.data == null) {
    throw new Error(
      `API response for printings is null or undefined. Status: ${res.status} ${res.statusText}. Response body: ${JSON.stringify(res.data)}`
    );
  }
  return res.data;
}
