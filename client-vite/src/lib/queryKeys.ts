/**
 * Centralized TanStack Query key factory to prevent key drift across queries and invalidations.
 */
export const queryKeys = {
  games: () => ["card-games"] as const,
  card: (cardId: number) => ["card", cardId] as const,
  cardPrintings: (cardId: number) => ["card", cardId, "printings"] as const,
  sparkline: (cardId: number, days: number) => ["sparkline", cardId, days] as const,
  collection: {
    all: () => ["collection"] as const,
    quantities: (printingIds: readonly number[]) =>
      ["collection", "quantities", ...printingIds] as const,
    setDetailPrefix: (setCode?: string) => ["collection", "set-detail", setCode] as const,
    setDetail: (setCode?: string, userId?: number | null, game?: string) =>
      ["collection", "set-detail", setCode, userId, game] as const,
  },
  printings: {
    set: (setCode?: string, game?: string) => ["printings", "set", setCode, game] as const,
  },
  wishlist: {
    all: () => ["wishlist"] as const,
    list: (
      userId: number | null | undefined,
      page: number,
      pageSize: number,
      q: string,
      game: string
    ) => ["wishlist", userId ?? null, page, pageSize, q, game] as const,
  },
  decks: {
    all: () => ["decks"] as const,
    detail: (deckId: number) => ["decks", deckId] as const,
  },
  value: {
    summary: (game?: string) => ["value", "summary", game ?? "all"] as const,
  },
  admin: {
    users: () => ["admin", "users"] as const,
  },
} as const;
