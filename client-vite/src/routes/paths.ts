export const paths = {
  cards: "cards",
  collection: "collection",
  collectionSetDetail: "collection/set/:setCode",
  wishlist: "wishlist",
  decks: "decks",
  adminSync: "admin/sync",
  adminSwccgSync: "admin/swccg-sync",
  adminPokemonSync: "admin/pokemon-sync",
  users: "users",
  value: "value",
} as const;

export type AppPath = (typeof paths)[keyof typeof paths];

/** Builds the relative route path for a specific deck builder page. */
export const deckBuilderPath = (deckId: number | string) => `${paths.decks}/${deckId}`;
