export type CardPrintingDetail = {
  id: number;
  set: string;
  number: string;
  rarity: string;
  style: string;
  imageUrl: string | null;
};

export type CardDetail = {
  cardId: number;
  name: string;
  game: string;
  cardType: string;
  description: string | null;
  printings: CardPrintingDetail[];
};

export type PrintingSummary = {
  printingId: number;
  setName: string;
  setCode?: string | null;
  number: string;
  rarity: string;
  imageUrl: string;
};

export type PricePoint = {
  d: string;
  p: number;
};

export type PriceHistory = {
  points: PricePoint[];
};

export type CollectionQuickAddResponse = {
  printingId: number;
  quantityOwned: number;
};

export type WishlistQuickAddResponse = {
  printingId: number;
  quantityWanted: number;
};

export type QuickAddVariables = {
  printingId: number;
  quantity: number;
};
