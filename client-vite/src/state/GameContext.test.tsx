import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { GameProvider } from "./GameProvider";
import { useGame } from "./useGame";
import { DEFAULT_GAME, STORAGE_KEY_ACTIVE_GAME, STORAGE_KEY_SELECTED_GAME } from "./GameContext";
import * as cardsApi from "@/features/cards/api/list";

function TestConsumer() {
  const { activeGame, setActiveGame, games, isLoadingGames } = useGame();
  return (
    <div>
      <div data-testid="active-game">{activeGame}</div>
      <div data-testid="games-list">{games.join(",")}</div>
      <div data-testid="is-loading">{String(isLoadingGames)}</div>
      <button type="button" onClick={() => setActiveGame("Lorcana")}>
        Select Lorcana
      </button>
      <button type="button" onClick={() => setActiveGame("")}>
        Select Empty
      </button>
    </div>
  );
}

function renderWithClient(ui: React.ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("GameProvider and useGame", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("defaults to Star Wars Unlimited when no storage or prop is set", () => {
    vi.spyOn(cardsApi, "fetchCardGames").mockResolvedValue(["Star Wars Unlimited"]);

    renderWithClient(
      <GameProvider>
        <TestConsumer />
      </GameProvider>
    );

    expect(screen.getByTestId("active-game")).toHaveTextContent(DEFAULT_GAME);
    expect(screen.getByTestId("active-game")).toHaveTextContent("Star Wars Unlimited");
  });

  it("restores active game from localStorage when available", () => {
    localStorage.setItem(STORAGE_KEY_ACTIVE_GAME, "Magic: The Gathering");
    vi.spyOn(cardsApi, "fetchCardGames").mockResolvedValue([
      "Star Wars Unlimited",
      "Magic: The Gathering",
    ]);

    renderWithClient(
      <GameProvider>
        <TestConsumer />
      </GameProvider>
    );

    expect(screen.getByTestId("active-game")).toHaveTextContent("Magic: The Gathering");
  });

  it("falls back to selected_game key in localStorage", () => {
    localStorage.setItem(STORAGE_KEY_SELECTED_GAME, "Lorcana");
    vi.spyOn(cardsApi, "fetchCardGames").mockResolvedValue(["Star Wars Unlimited", "Lorcana"]);

    renderWithClient(
      <GameProvider>
        <TestConsumer />
      </GameProvider>
    );

    expect(screen.getByTestId("active-game")).toHaveTextContent("Lorcana");
  });

  it("updates activeGame and persists to localStorage on setActiveGame", () => {
    vi.spyOn(cardsApi, "fetchCardGames").mockResolvedValue(["Star Wars Unlimited", "Lorcana"]);

    renderWithClient(
      <GameProvider>
        <TestConsumer />
      </GameProvider>
    );

    expect(screen.getByTestId("active-game")).toHaveTextContent("Star Wars Unlimited");

    act(() => {
      screen.getByRole("button", { name: "Select Lorcana" }).click();
    });

    expect(screen.getByTestId("active-game")).toHaveTextContent("Lorcana");
    expect(localStorage.getItem(STORAGE_KEY_ACTIVE_GAME)).toBe("Lorcana");
    expect(localStorage.getItem(STORAGE_KEY_SELECTED_GAME)).toBe("Lorcana");
  });

  it("does not allow setting an empty game", () => {
    vi.spyOn(cardsApi, "fetchCardGames").mockResolvedValue(["Star Wars Unlimited"]);

    renderWithClient(
      <GameProvider>
        <TestConsumer />
      </GameProvider>
    );

    act(() => {
      screen.getByRole("button", { name: "Select Empty" }).click();
    });

    // Should remain Star Wars Unlimited
    expect(screen.getByTestId("active-game")).toHaveTextContent("Star Wars Unlimited");
  });

  it("throws an error when useGame is used outside GameProvider", () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(() => render(<TestConsumer />)).toThrow("useGame must be inside GameProvider");
    spy.mockRestore();
  });
});
