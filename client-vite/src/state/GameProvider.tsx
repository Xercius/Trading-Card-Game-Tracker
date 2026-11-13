import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { fetchCardGames } from "@/features/cards/api/list";
import { queryKeys } from "@/lib/queryKeys";
import {
  DEFAULT_GAME,
  GameContext,
  type GameContextValue,
  STORAGE_KEY_ACTIVE_GAME,
} from "./GameContext";

const LEGACY_SELECTED_GAME_KEY = "selected_game";

function getInitialGame(defaultProp?: string): string {
  if (defaultProp && defaultProp.trim()) {
    return defaultProp.trim();
  }
  try {
    if (typeof window !== "undefined" && window.localStorage) {
      const stored = window.localStorage.getItem(STORAGE_KEY_ACTIVE_GAME);
      if (stored && stored.trim()) {
        return stored.trim();
      }

      const legacy = window.localStorage.getItem(LEGACY_SELECTED_GAME_KEY);
      if (legacy && legacy.trim()) {
        const migrated = legacy.trim();
        window.localStorage.setItem(STORAGE_KEY_ACTIVE_GAME, migrated);
        window.localStorage.removeItem(LEGACY_SELECTED_GAME_KEY);
        return migrated;
      }
    }
  } catch {
    // Ignore localStorage access failures
  }
  return DEFAULT_GAME;
}

type GameProviderProps = {
  children: ReactNode;
  defaultGame?: string;
};

export function GameProvider({ children, defaultGame }: GameProviderProps) {
  const [activeGame, setActiveGameState] = useState<string>(() => getInitialGame(defaultGame));

  const gamesQuery = useQuery({
    queryKey: queryKeys.games(),
    queryFn: fetchCardGames,
    staleTime: 5 * 60 * 1000,
  });

  const games = useMemo(() => {
    const fetched = gamesQuery.data ?? [];
    const set = new Set<string>();
    if (DEFAULT_GAME) set.add(DEFAULT_GAME);
    if (activeGame) set.add(activeGame);
    for (const g of fetched) {
      if (g && g.trim()) set.add(g.trim());
    }
    return Array.from(set).sort((a, b) => a.localeCompare(b));
  }, [gamesQuery.data, activeGame]);

  const setActiveGame = useCallback((nextGame: string) => {
    const trimmed = nextGame?.trim();
    if (!trimmed) return;
    setActiveGameState(trimmed);
    try {
      if (typeof window !== "undefined" && window.localStorage) {
        window.localStorage.setItem(STORAGE_KEY_ACTIVE_GAME, trimmed);
      }
    } catch {
      // Ignore localStorage access failures
    }
  }, []);

  const value = useMemo<GameContextValue>(
    () => ({
      activeGame,
      setActiveGame,
      games,
      isLoadingGames: gamesQuery.isLoading,
    }),
    [activeGame, setActiveGame, games, gamesQuery.isLoading]
  );

  return <GameContext.Provider value={value}>{children}</GameContext.Provider>;
}
