export type CardPrintingImages = {
  /** The existing image URL represents the front face. */
  imageUrl: string | null;
  backImageUrl?: string | null;
  imageOrientation?: "horizontal" | "vertical" | null;
  backImageOrientation?: "horizontal" | "vertical" | null;
};

export type CardPrintingDetail = CardPrintingImages & {
  id: number;
  set: string;
  number: string;
  rarity: string;
  style: string;
};

export type CardDetail = {
  cardId: number;
  name: string;
  game: string;
  cardType: string;
  description: string | null;
  printings: CardPrintingDetail[];
};

export type PrintingSummary = CardPrintingImages & {
  printingId: number;
  setName: string;
  setCode?: string | null;
  number: string;
  rarity: string;
  imageUrl: string;
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
