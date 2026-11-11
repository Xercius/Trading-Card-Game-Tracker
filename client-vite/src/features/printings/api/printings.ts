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
  cost?: number | null;
  subtitle?: string | null;
  destiny?: string | null;
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
  aspectMethod?: "or" | "and" | "exclude";
  availableAspects?: string[];
  traits?: string[];
  traitMethod?: "or" | "and" | "exclude";
  keywords?: string[];
  keywordMethod?: "or" | "and" | "exclude";
  side?: string;
  personas?: string[];
  subTypes?: string[];
  destinies?: string[];
  deploys?: string[];
  forfeits?: string[];
  powers?: string[];
  abilities?: string[];
  lightSideIcons?: string[];
  darkSideIcons?: string[];
  hps?: string[];
  stages?: string[];
  sort?: string;
  page?: number;
  pageSize?: number;
  uniqueCards?: boolean;
};

export type PrintingsResult = {
  items: PrintingListItem[];
  total: number;
};

export async function fetchPrintings(query: PrintingQuery): Promise<PrintingsResult> {
  const params = new URLSearchParams();
  if (query.q) params.set("q", query.q);
  if (query.game?.length) params.set("game", query.game.join(","));
  if (query.set?.length) params.set("set", query.set.join(","));
  if (query.rarity?.length) params.set("rarity", query.rarity.join(","));
  if (query.types?.length) params.set("type", query.types.join(","));
  if (query.arenas?.length) params.set("arena", query.arenas.join(","));
  if (query.aspects?.length) params.set("aspect", query.aspects.join(","));
  if (query.aspectMethod && query.aspectMethod !== "or")
    params.set("aspectMethod", query.aspectMethod);
  if (query.availableAspects !== undefined)
    params.set("availableAspects", query.availableAspects.join(","));
  if (query.traits?.length) params.set("trait", query.traits.join(","));
  if (query.traitMethod && query.traitMethod !== "or") params.set("traitMethod", query.traitMethod);
  if (query.keywords?.length) params.set("keyword", query.keywords.join(","));
  if (query.keywordMethod && query.keywordMethod !== "or")
    params.set("keywordMethod", query.keywordMethod);
  if (query.side) params.set("side", query.side);
  if (query.personas?.length) params.set("persona", query.personas.join(","));
  if (query.subTypes?.length) params.set("subType", query.subTypes.join(","));
  if (query.destinies?.length) params.set("destiny", query.destinies.join(","));
  if (query.deploys?.length) params.set("deploy", query.deploys.join(","));
  if (query.forfeits?.length) params.set("forfeit", query.forfeits.join(","));
  if (query.powers?.length) params.set("power", query.powers.join(","));
  if (query.abilities?.length) params.set("ability", query.abilities.join(","));
  if (query.lightSideIcons?.length) params.set("lightSideIcon", query.lightSideIcons.join(","));
  if (query.darkSideIcons?.length) params.set("darkSideIcon", query.darkSideIcons.join(","));
  if (query.hps?.length) params.set("hp", query.hps.join(","));
  if (query.stages?.length) params.set("stage", query.stages.join(","));
  if (query.sort) params.set("sort", query.sort);
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  if (query.uniqueCards) params.set("uniqueCards", "true");

  const paramString = params.toString();
  const res = await api.get<PrintingListItem[]>(
    `cards/printings${paramString ? `?${paramString}` : ""}`
  );
  const totalHeader = res.headers?.["x-total-count"] ?? res.headers?.["X-Total-Count"];
  const parsedTotal = totalHeader ? Number(totalHeader) : NaN;
  const total = !Number.isNaN(parsedTotal) ? parsedTotal : res.data.length;
  return { items: res.data, total };
}

/** Fetch every page matching a printing query. */
export async function fetchAllPrintings(query: PrintingQuery): Promise<PrintingListItem[]> {
  const pageSize = 500;
  const firstPage = await fetchPrintings({ ...query, page: 1, pageSize });
  const items = [...firstPage.items];

  for (let page = 2; items.length < firstPage.total; page += 1) {
    const nextPage = await fetchPrintings({ ...query, page, pageSize });
    if (nextPage.items.length === 0) break;
    items.push(...nextPage.items);
  }

  return items;
}
