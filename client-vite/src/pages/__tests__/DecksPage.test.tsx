import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { createRoot } from "react-dom/client";
import { act } from "react";
import { fireEvent, waitFor, within, screen } from "@testing-library/react";
import DecksPage from "../DecksPage";
import http from "@/lib/http";

vi.mock("@/state/useGame", () => ({
  useGame: () => ({
    activeGame: "Star Wars Unlimited",
    setActiveGame: vi.fn(),
    games: ["Star Wars Unlimited", "Star Wars CCG"],
    isLoadingGames: false,
  }),
}));

vi.mock("@/features/decks/components/DeckConstructionModal", () => ({
  DeckConstructionModal: () => null,
}));

const mockDecksResponse = {
  items: [
    {
      id: 1,
      userId: 1,
      game: "Star Wars Unlimited",
      name: "Sabine Aggro",
      description: "Fast red/yellow aggro deck",
      format: "Premier",
      aspects: ["Aggression", "Heroism"],
      createdUtc: "2026-11-01T00:00:00Z",
      updatedUtc: "2026-11-02T00:00:00Z",
      cardCount: 52,
      mainDeckCardCount: 50,
      featuredCards: [
        {
          cardPrintingId: 101,
          cardName: "Sabine Wren — Galvanized Revolutionary",
          imageUrl: "https://example.com/sabine.png",
          cardType: "Leader",
          isHorizontal: true,
        },
      ],
      baseCard: {
        cardPrintingId: 102,
        cardName: "Energy Conversion Lab",
        imageUrl: "https://example.com/ecl.png",
        cardType: "Base",
        isHorizontal: true,
      },
    },
  ],
  total: 1,
  page: 1,
  pageSize: 50,
};

function createClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

describe("DecksPage", () => {
  let container: HTMLDivElement;
  let root: ReturnType<typeof createRoot>;

  beforeEach(() => {
    container = document.createElement("div");
    document.body.appendChild(container);
    root = createRoot(container);

    vi.spyOn(http, "get").mockImplementation((url) => {
      const u = typeof url === "string" ? url : "";
      if (u.includes("cards-with-availability")) {
        return Promise.resolve({ data: [] } as never);
      }
      if (u.includes("decks")) {
        return Promise.resolve({ data: mockDecksResponse } as never);
      }
      return Promise.resolve({ data: [] } as never);
    });
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    vi.restoreAllMocks();
  });

  it("renders page header, format badge, aspect icons, leader and base previews", async () => {
    const client = createClient();

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <MemoryRouter>
            <DecksPage />
          </MemoryRouter>
        </QueryClientProvider>
      );
    });

    await waitFor(() => {
      expect(container.textContent).toContain("Star Wars Unlimited Decks");
      expect(container.textContent).toContain("Sabine Aggro");
      expect(container.textContent).toContain("Premier");
      expect(container.textContent).toContain("✓ Legal");
      expect(container.textContent).toContain("52 cards");
      expect(container.textContent).toContain("Leaders & Base");
      expect(container.textContent).not.toContain("Sabine Wren");
      expect(container.textContent).not.toContain("Energy Conversion Lab");
    });

    const leaderImage = within(container).getByRole("img", {
      name: "Sabine Wren — Galvanized Revolutionary",
    });
    expect(leaderImage).toHaveStyle({ width: "144px", height: "104px" });

    const baseImage = within(container).getByRole("img", {
      name: "Energy Conversion Lab",
    });
    expect(baseImage).toHaveStyle({ width: "144px", height: "104px" });

    // Aspect emblems
    expect(within(container).getByRole("img", { name: "Aggression" })).toBeInTheDocument();
    expect(within(container).getByRole("img", { name: "Heroism" })).toBeInTheDocument();
  });

  it("opens delete confirmation modal when delete icon is clicked", async () => {
    const client = createClient();

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <MemoryRouter>
            <DecksPage />
          </MemoryRouter>
        </QueryClientProvider>
      );
    });

    await waitFor(() => {
      expect(container.textContent).toContain("Sabine Aggro");
    });

    const deleteBtn = container.querySelector(
      "button[aria-label='Delete deck Sabine Aggro']"
    ) as HTMLButtonElement;
    expect(deleteBtn).not.toBeNull();

    await act(async () => {
      fireEvent.click(deleteBtn);
    });

    expect(document.body.textContent).toContain("Delete Deck?");
    expect(document.body.textContent).toContain('Are you sure you want to delete "Sabine Aggro"?');
  });

  it("restores pagination for deck results beyond the first page", async () => {
    const client = createClient();
    vi.spyOn(http, "get").mockImplementation(() =>
      Promise.resolve({ data: { ...mockDecksResponse, total: 101 } } as never)
    );

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <MemoryRouter>
            <DecksPage />
          </MemoryRouter>
        </QueryClientProvider>
      );
    });

    expect(await screen.findByText("Page 1 of 3")).toBeInTheDocument();
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Next" }));
    });

    expect(await screen.findByText("Page 2 of 3")).toBeInTheDocument();
    expect(http.get).toHaveBeenLastCalledWith("decks", {
      params: { game: "Star Wars Unlimited", name: undefined, page: 2, pageSize: 50 },
    });
  });

  it("shows a friendly delete error and clears it when the dialog is closed", async () => {
    const client = createClient();
    vi.spyOn(http, "delete").mockRejectedValue(new Error("server detail"));

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <MemoryRouter>
            <DecksPage />
          </MemoryRouter>
        </QueryClientProvider>
      );
    });

    await screen.findByText("Sabine Aggro");
    fireEvent.click(screen.getByRole("button", { name: "Delete deck Sabine Aggro" }));
    const cancelButton = await screen.findByRole("button", { name: "Cancel" });
    await waitFor(() => expect(document.activeElement).toBe(cancelButton));
    fireEvent.click(screen.getByRole("button", { name: "Delete Deck" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Could not delete the deck. Please try again."
    );
    expect(screen.getByText("Sabine Aggro")).toBeInTheDocument();

    fireEvent.click(cancelButton);
    fireEvent.click(screen.getByRole("button", { name: "Delete deck Sabine Aggro" }));
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("opens CreateDeckDialog when + Create Deck is clicked and submits deck", async () => {
    const client = createClient();
    const postSpy = vi.spyOn(http, "post").mockResolvedValue({
      data: {
        id: 2,
        userId: 1,
        game: "Star Wars Unlimited",
        name: "Boba Yellow",
        description: "Twin Suns variant",
        format: "Twin Suns",
        side: null,
        createdUtc: "2026-11-04T00:00:00Z",
        updatedUtc: null,
        cardCount: 0,
      },
    } as never);

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <MemoryRouter>
            <DecksPage />
          </MemoryRouter>
        </QueryClientProvider>
      );
    });

    const buttons = Array.from(container.querySelectorAll("button"));
    const targetBtn = buttons.find((b) => b.textContent?.includes("Create Deck"));
    expect(targetBtn).not.toBeUndefined();

    await act(async () => {
      fireEvent.click(targetBtn!);
    });

    // CreateDeckDialog opens in portal
    expect(document.body.textContent).toContain("Create New Deck");

    const nameInput = document.body.querySelector("input#deck-name-input") as HTMLInputElement;
    expect(nameInput).not.toBeNull();

    await act(async () => {
      fireEvent.change(nameInput, { target: { value: "Boba Yellow" } });
    });

    const twinSunsBtn = screen.getByRole("button", { name: /Twin Suns/i });
    expect(twinSunsBtn).not.toBeNull();

    await act(async () => {
      fireEvent.click(twinSunsBtn);
    });

    const submitBtn = screen.getByRole("button", { name: "Create & Build Deck" });
    expect(submitBtn).not.toBeNull();

    await act(async () => {
      fireEvent.click(submitBtn);
    });

    expect(postSpy).toHaveBeenCalledWith(
      "decks",
      expect.objectContaining({
        game: "Star Wars Unlimited",
        name: "Boba Yellow",
        format: "Twin Suns",
      })
    );
  });
});
