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

  it("renders the game logo buttons in the required order with Star Wars Unlimited selected by default", async () => {
    renderHeader();

    const expectedGames = [
      "Star Wars Unlimited",
      "Star Wars CCG",
      "Hellbreak TCG",
      "Magic The Gathering",
      "Pokemon TCG",
    ];

    const buttons = expectedGames.map((name) =>
      screen.getByRole("button", { name: new RegExp(`^${name}`, "i") })
    );

    // Verify presence and ordering
    buttons.forEach((btn) => expect(btn).toBeInTheDocument());
    for (let i = 0; i < buttons.length - 1; i++) {
      expect(
        buttons[i].compareDocumentPosition(buttons[i + 1]) & Node.DOCUMENT_POSITION_FOLLOWING
      ).toBeTruthy();
    }

    // Star Wars Unlimited is active and selectable
    const swuButton = buttons[0];
    expect(swuButton).toHaveAttribute("aria-pressed", "true");
    expect(swuButton).toBeEnabled();

    // The other games are disabled (not selectable)
    for (let i = 1; i < buttons.length; i++) {
      expect(buttons[i]).toBeDisabled();
      expect(buttons[i]).toHaveAttribute("aria-pressed", "false");
    }
  });

  it("renders the search bar to the right of the Admin dropdown in the top row", async () => {
    renderHeader();

    const adminTrigger = screen.getByRole("button", { name: /^Admin/i });
    const searchInput = screen.getByPlaceholderText("Search cards, decks...");

    expect(adminTrigger).toBeInTheDocument();
    expect(searchInput).toBeInTheDocument();

    // In DOM order, search bar follows the Admin dropdown
    expect(
      adminTrigger.compareDocumentPosition(searchInput) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy();
  });

  it("keeps the mobile drawer open when debounced search updates the URL", async () => {
    vi.useFakeTimers();
    let currentLoc: Location | null = null;
    renderHeader(["/cards"], (loc) => {
      currentLoc = loc;
    });

    fireEvent.click(screen.getByRole("button", { name: "Toggle navigation" }));
    const mobileSearch = screen.getAllByPlaceholderText("Search cards, decks...").at(-1)!;
    fireEvent.change(mobileSearch, { target: { value: "vader" } });

    act(() => {
      vi.advanceTimersByTime(350);
    });

    expect(new URLSearchParams(currentLoc!.search).get("q")).toBe("vader");
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(mobileSearch).toHaveValue("vader");
    vi.useRealTimers();
  });

  it("renders a text brand when no logo asset is available", () => {
    renderHeader();

    expect(screen.getByRole("link", { name: "TCG Tracker" })).toHaveTextContent("TCG Tracker");
  });

  it("allows selecting Star Wars Unlimited and updates state and localStorage", async () => {
    renderHeader();

    const swuButton = screen.getByRole("button", { name: new RegExp(`^Star Wars Unlimited`, "i") });
    fireEvent.click(swuButton);

    expect(swuButton).toHaveAttribute("aria-pressed", "true");
    expect(localStorage.getItem(STORAGE_KEY_ACTIVE_GAME)).toBe("Star Wars Unlimited");
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
