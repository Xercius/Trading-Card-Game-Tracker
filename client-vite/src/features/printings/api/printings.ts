import { api } from "@/lib/api";

export type PrintingListItem = {
  printingId: number | string;
  cardId: number | string;
  cardName: string;
  game: string;
  setName: string;
  setCode?: string | null;
  number?: string | null;
  rarity?: string | null;
  imageUrl?: string | null;
  style?: string | null;
  releaseDate?: string | null;
  backImageUrl?: string | null;
  imageOrientation?: "horizontal" | "vertical" | null;
  backImageOrientation?: "horizontal" | "vertical" | null;
  cardTypes?: string[];
  aspects?: { name: string; count: number }[];
};

export type PrintingQuery = {
  q?: string;
  game?: string[];
  set?: string[];
  rarity?: string[];
  types?: string[];
  arenas?: string[];
  aspects?: string[];
  traits?: string[];
  page?: number;
  pageSize?: number;
  uniqueCards?: boolean;
};

export async function fetchPrintings(query: PrintingQuery): Promise<PrintingListItem[]> {
  const params = new URLSearchParams();
  if (query.q) params.set("q", query.q);
  if (query.game?.length) params.set("game", query.game.join(","));
  if (query.set?.length) params.set("set", query.set.join(","));
  if (query.rarity?.length) params.set("rarity", query.rarity.join(","));
  if (query.types?.length) params.set("type", query.types.join(","));
  if (query.arenas?.length) params.set("arena", query.arenas.join(","));
  if (query.aspects?.length) params.set("aspect", query.aspects.join(","));
  if (query.traits?.length) params.set("trait", query.traits.join(","));
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  if (query.uniqueCards) params.set("uniqueCards", "true");

  const paramString = params.toString();
  const res = await api.get<PrintingListItem[]>(
    `cards/printings${paramString ? `?${paramString}` : ""}`
  );
  return res.data;
}
