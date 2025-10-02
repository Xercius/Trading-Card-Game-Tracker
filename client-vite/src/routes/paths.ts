export const paths = {
  cards: '/cards',
  collection: '/collection',
  wishlist: '/wishlist',
  decks: '/decks',
  adminImport: '/admin/import',
  users: '/users',
  value: '/value',
} as const;

export type AppPath = (typeof paths)[keyof typeof paths];
