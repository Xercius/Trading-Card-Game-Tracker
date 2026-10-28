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
  artist?: string | null;
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
  cardType2?: string | null;
  cardTypes?: string[];
  arena?: string | null;
  aspects?: CardAspect[];
  traits?: string[];
  text: string | null;
  textStyled?: string | null;
  epicAction?: string | null;
  epicActionStyled?: string | null;
  deployBox?: string | null;
  deployBoxStyled?: string | null;
  cost?: number | null;
  hp?: number | null;
  power?: number | null;
  upgradeHp?: number | null;
  upgradePower?: number | null;
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
  artist?: string | null;
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
