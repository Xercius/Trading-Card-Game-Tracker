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
  setCode?: string | null;
  number: string;
  rarity: string;
  style: string;
  releaseDate?: string | null;
};

export type CardAspect = {
  name: string;
  count: number;
};

export type CardDetail = {
  id: number;
  name: string;
  game: string;
  cardType: string;
  cardTypes?: string[];
  aspects?: CardAspect[];
  description: string | null;
  printings: CardPrintingDetail[];
};

export type PrintingSummary = CardPrintingImages & {
  printingId: number;
  setName: string;
  setCode?: string | null;
  number: string;
  rarity: string;
  style?: string;
  releaseDate?: string | null;
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
