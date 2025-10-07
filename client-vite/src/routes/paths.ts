export const paths = {
  cards: "cards",
  collection: "collection",
  wishlist: "wishlist",
  decks: "decks",
  adminImport: "admin/import",
  users: "users",
  value: "value",
} as const;

if (import.meta.env.DEV) {
  for (const [key, value] of Object.entries(paths)) {
    if (typeof value === "string" && value.startsWith("/")) {
      // eslint-disable-next-line no-console
      console.warn(
        `[paths] "${key}" starts with "/". Make it relative: "${value.slice(1)}"`
      );
    }
  }
}

export type AppPath = (typeof paths)[keyof typeof paths];

export const deckBuilderPath = (deckId: number | string) => `${paths.decks}/${deckId}`;
