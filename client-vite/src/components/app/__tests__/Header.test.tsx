import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, useLocation } from "react-router-dom";
import type { Location } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Header from "../Header";
import { UserProvider } from "@/state/UserProvider";
import { GameProvider } from "@/state/GameProvider";
import { STORAGE_KEY_ACTIVE_GAME } from "@/state/GameContext";
import * as cardsApi from "@/features/cards/api/list";

function LocationProbe({ onLocation }: { onLocation: (loc: Location) => void }) {
  const loc = useLocation();
  onLocation(loc);
  return null;
}

function renderHeader(initialEntries = ["/cards"], onLocation?: (loc: Location) => void) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={initialEntries}>
        <UserProvider>
          <GameProvider>
            <Header />
            {onLocation && <LocationProbe onLocation={onLocation} />}
          </GameProvider>
        </UserProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("Header", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
    vi.spyOn(cardsApi, "fetchCardGames").mockResolvedValue([
      "Star Wars Unlimited",
      "Lorcana",
      "Magic",
    ]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("renders the game filter to the left of the search bar with Star Wars Unlimited selected by default", async () => {
    renderHeader();

    const gameSelect = screen.getByRole("combobox", { name: "Game" });
    const searchInput = screen.getByPlaceholderText("Search cards, decks...");

    expect(gameSelect).toBeInTheDocument();
    expect(searchInput).toBeInTheDocument();
    expect(gameSelect).toHaveTextContent("Star Wars Unlimited");

    // Check order in DOM: gameSelect before searchInput
    expect(
      gameSelect.compareDocumentPosition(searchInput) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy();
  });

  it("restores the last selected game from localStorage", async () => {
    localStorage.setItem(STORAGE_KEY_ACTIVE_GAME, "Lorcana");
    renderHeader();

    const gameSelect = screen.getByRole("combobox", { name: "Game" });
    expect(gameSelect).toHaveTextContent("Lorcana");
  });

  it("allows selecting another game and updates state and localStorage", async () => {
    renderHeader();

    const gameSelect = screen.getByRole("combobox", { name: "Game" });
    fireEvent.click(gameSelect);

    const lorcanaOption = await screen.findByRole("option", { name: "Lorcana" });
    fireEvent.click(lorcanaOption);

    expect(gameSelect).toHaveTextContent("Lorcana");
    expect(localStorage.getItem(STORAGE_KEY_ACTIVE_GAME)).toBe("Lorcana");
  });

  it("updates search query on debounce and resets page to 1", async () => {
    vi.useFakeTimers();
    let currentLoc: Location | null = null;
    renderHeader(["/cards?page=3"], (loc) => {
      currentLoc = loc;
    });

    const searchInput = screen.getByPlaceholderText("Search cards, decks...");
    fireEvent.change(searchInput, { target: { value: "vader" } });

    act(() => {
      vi.advanceTimersByTime(350);
    });

    const params = new URLSearchParams(currentLoc!.search);
    expect(params.get("q")).toBe("vader");
    expect(params.get("page")).toBe("1");
    vi.useRealTimers();
  });

  it("immediately submits search on Enter key and resets page to 1", async () => {
    let currentLoc: Location | null = null;
    renderHeader(["/cards?page=2"], (loc) => {
      currentLoc = loc;
    });

    const searchInput = screen.getByPlaceholderText("Search cards, decks...");
    fireEvent.change(searchInput, { target: { value: "luke" } });
    fireEvent.keyDown(searchInput, { key: "Enter" });

    await waitFor(() => {
      const params = new URLSearchParams(currentLoc!.search);
      expect(params.get("q")).toBe("luke");
      expect(params.get("page")).toBe("1");
    });
  });

  it("clears search query and resets page to 1 on Clear click", async () => {
    let currentLoc: Location | null = null;
    renderHeader(["/cards?q=vader&page=2"], (loc) => {
      currentLoc = loc;
    });

    const searchInput = screen.getByPlaceholderText("Search cards, decks...");
    expect(searchInput).toHaveValue("vader");

    const clearButton = screen.getByRole("button", { name: "Clear" });
    fireEvent.click(clearButton);

    await waitFor(() => {
      const params = new URLSearchParams(currentLoc!.search);
      expect(params.get("q")).toBeNull();
      expect(params.get("page")).toBe("1");
    });
  });
});
