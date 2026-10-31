import { useContext } from "react";
import { GameContext } from "./GameContext";

/**
 * Accesses the global game selection state and list of available games.
 */
export function useGame() {
  const ctx = useContext(GameContext);
  if (!ctx) throw new Error("useGame must be inside GameProvider");
  return ctx;
}
