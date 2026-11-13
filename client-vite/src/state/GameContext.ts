import { createContext } from "react";

export const DEFAULT_GAME = "Star Wars CCG";
export const STORAGE_KEY_ACTIVE_GAME = "active_game";

export type GameContextValue = {
  activeGame: string;
  setActiveGame: (game: string) => void;
  games: string[];
  isLoadingGames: boolean;
};

export const GameContext = createContext<GameContextValue | null>(null);
