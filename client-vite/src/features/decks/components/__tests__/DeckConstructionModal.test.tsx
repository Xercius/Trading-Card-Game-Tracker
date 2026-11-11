import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createRoot } from "react-dom/client";
import { act } from "react";
import { fireEvent, waitFor, screen } from "@testing-library/react";
import { DeckConstructionModal } from "../DeckConstructionModal";
import { api } from "@/lib/api";
import type { DeckCardWithAvailability } from "../../api";

const mockDeckDetails = {
  id: 1,
  userId: 1,
  game: "Star Wars Unlimited",
  name: "Boba Fett Midrange",
  description: "Cunning Boba deck",
  createdUtc: "2026-11-01T00:00:00Z",
  updatedUtc: "2026-11-02T00:00:00Z",
  cardCount: 2,
};

const mockDeckCards = [
  {
    printingId: 10,
    cardName: "Boba Fett — Collecting the Bounty",
    imageUrl: "https://example.com/boba.png",
    quantityInDeck: 1,
    availability: 1,
    availabilityWithProxies: 1,
    cardType: "Leader",
    isHorizontal: true,
  },
  {
    printingId: 20,
    cardName: "Fett's Firespray",
    imageUrl: "https://example.com/firespray.png",
    quantityInDeck: 2,
    availability: 3,
    availabilityWithProxies: 3,
    cardType: "Unit",
    isHorizontal: false,
  },
];

const mockPrintings = [
  {
    printingId: 30,
    cardId: 300,
    cardName: "Surprise Strike",
    game: "Star Wars Unlimited",
    setName: "Spark of Rebellion",
    setCode: "SOR",
    number: "220",
    rarity: "Common",
    cardTypes: ["Event"],
    imageUrl: "https://example.com/strike.png",
    imageOrientation: "vertical" as const,
  },
  {
    printingId: 40,
    cardId: 400,
    cardName: "Luke Skywalker",
    game: "Star Wars Unlimited",
    cardTypes: ["Leader"],
    imageUrl: "https://example.com/luke.png",
    imageOrientation: null,
  },
  {
    printingId: 50,
    cardId: 500,
    cardName: "Luke Skywalker Deployed",
    game: "Star Wars Unlimited",
    cardTypes: ["Leader", "Unit"],
    imageUrl: "https://example.com/luke-deployed.png",
    imageOrientation: "vertical" as const,
  },
  {
    printingId: 60,
    cardId: 600,
    cardName: "Echo Base",
    game: "Star Wars Unlimited",
    cardTypes: ["Base"],
    imageUrl: "https://example.com/echo-base.png",
    imageOrientation: "horizontal" as const,
  },
];

function createClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

describe("DeckConstructionModal", () => {
  let container: HTMLDivElement;
  let root: ReturnType<typeof createRoot>;
  let persistedDeckCards: DeckCardWithAvailability[];
  let deckDetails: typeof mockDeckDetails & { format: string };

  beforeEach(() => {
    container = document.createElement("div");
    document.body.appendChild(container);
    root = createRoot(container);
    persistedDeckCards = [...mockDeckCards];
    deckDetails = { ...mockDeckDetails, format: "Premier" };

    vi.spyOn(api, "get").mockImplementation((url) => {
      const u = String(url);
      if (u.includes("cards-with-availability")) {
        return Promise.resolve({ data: persistedDeckCards } as never);
      }
      if (u.includes("decks/1")) {
        return Promise.resolve({ data: deckDetails } as never);
      }
      if (u.includes("printings")) {
        return Promise.resolve({
          data: mockPrintings,
          headers: { "x-total-count": String(mockPrintings.length) },
        } as never);
      }
      if (u.includes("facets")) {
        return Promise.resolve({ data: { games: [], sets: [], rarities: [], types: [] } } as never);
      }
      return Promise.resolve({ data: {} } as never);
    });
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    vi.restoreAllMocks();
  });

  it("renders modal header with editable name, game badge, and deck cards", async () => {
    const client = createClient();
    const handleClose = vi.fn();

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <DeckConstructionModal
            deckId={1}
            isOpen={true}
            onClose={handleClose}
            game="Star Wars Unlimited"
          />
        </QueryClientProvider>
      );
    });

    await waitFor(() => {
      expect(document.body.textContent).toContain("Boba Fett Midrange");
      expect(document.body.textContent).toContain("Cunning Boba deck");
      expect(document.body.textContent).toContain("Star Wars Unlimited");
      expect(document.body.textContent).toContain("Boba Fett — Collecting the Bounty");
      expect(document.body.textContent).toContain("Fett's Firespray");
      expect(document.body.textContent).toContain("3 total"); // 1 + 2 cards
    });

    const closeBtn = document.body.querySelector(
      "button[aria-label='Close modal']"
    ) as HTMLButtonElement;
    expect(closeBtn).not.toBeNull();
    await act(async () => {
      fireEvent.click(closeBtn);
    });
    expect(handleClose).toHaveBeenCalled();
  });

  it("calls quantity-delta when adding a card from search results", async () => {
    const client = createClient();
    const postSpy = vi.spyOn(api, "post").mockResolvedValue({ data: {} } as never);

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <DeckConstructionModal
            deckId={1}
            isOpen={true}
            onClose={vi.fn()}
            game="Star Wars Unlimited"
          />
        </QueryClientProvider>
      );
    });

    await waitFor(() => {
      expect(document.body.textContent).toContain("Surprise Strike");
    });

    const addBtn = Array.from(document.body.querySelectorAll("button")).find((b) =>
      b.textContent?.includes("+ Add")
    );
    expect(addBtn).not.toBeUndefined();

    await act(async () => {
      fireEvent.click(addBtn!);
    });

    expect(postSpy).toHaveBeenCalledWith(
      "decks/1/cards/quantity-delta",
      expect.objectContaining({
        printingId: 30,
        qtyDelta: 1,
      }),
      expect.anything()
    );
  });

  it("matches the set-page thumbnail sizing and centered slots in both card lists", async () => {
    const client = createClient();

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <DeckConstructionModal deckId={1} isOpen={true} onClose={vi.fn()} />
        </QueryClientProvider>
      );
    });

    const leader = await screen.findByRole("img", {
      name: "Boba Fett — Collecting the Bounty",
    });
    expect(leader).toHaveStyle({ width: "44px", height: "32px" });
    expect(screen.getByRole("img", { name: "Fett's Firespray" })).toHaveStyle({
      width: "32px",
      height: "44px",
    });

    const searchLeader = await screen.findByRole("img", { name: "Luke Skywalker" });
    expect(searchLeader).toHaveStyle({ width: "44px", height: "32px" });
    expect(screen.getByRole("img", { name: "Echo Base" })).toHaveStyle({
      width: "44px",
      height: "32px",
    });
    expect(screen.getByRole("img", { name: "Luke Skywalker Deployed" })).toHaveStyle({
      width: "32px",
      height: "44px",
    });
    expect(screen.getByRole("img", { name: "Surprise Strike" })).toHaveStyle({
      width: "32px",
      height: "44px",
    });

    for (const name of [
      "Boba Fett — Collecting the Bounty",
      "Fett's Firespray",
      "Luke Skywalker",
      "Echo Base",
      "Luke Skywalker Deployed",
      "Surprise Strike",
    ]) {
      const image = screen.getByRole("img", { name });
      expect(image.parentElement?.parentElement).toHaveClass(
        "flex",
        "items-center",
        "justify-center",
        "w-12",
        "h-11"
      );
    }
  });

  it("keeps an unowned card and its quantity after saving and refetching", async () => {
    persistedDeckCards = [];
    const client = createClient();
    vi.spyOn(api, "post").mockImplementation(async () => {
      const currentQuantity = persistedDeckCards[0]?.quantityInDeck ?? 0;
      persistedDeckCards = [
        {
          printingId: 30,
          cardName: "Surprise Strike",
          imageUrl: "https://example.com/strike.png",
          quantityInDeck: currentQuantity + 1,
          availability: 0,
          availabilityWithProxies: 0,
          cardType: "Event",
          isHorizontal: false,
        },
      ];
      return { data: persistedDeckCards[0] } as never;
    });

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <DeckConstructionModal deckId={1} isOpen={true} onClose={vi.fn()} />
        </QueryClientProvider>
      );
    });

    const addButton = (await screen.findAllByRole("button", { name: "+ Add" }))[0];
    fireEvent.click(addButton);
    await waitFor(() => {
      expect(document.body.textContent).toContain("1 total");
      expect(document.body.textContent).toContain("Avail: 0");
      expect(document.body.textContent).toContain("Event");
      expect(client.isFetching()).toBe(0);
      expect(client.isMutating()).toBe(0);
    });

    fireEvent.click(screen.getByRole("button", { name: "Increase Surprise Strike quantity" }));
    await waitFor(() => {
      expect(document.body.textContent).toContain("2 total");
      expect(client.isFetching()).toBe(0);
      expect(client.isMutating()).toBe(0);
    });
    expect(persistedDeckCards[0].quantityInDeck).toBe(2);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("rolls back a failed addition and shows a dismissible error notification", async () => {
    persistedDeckCards = [];
    const client = createClient();
    vi.spyOn(api, "post").mockRejectedValue({
      isAxiosError: true,
      response: { status: 500, data: { title: "A server error occurred." } },
    });

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <DeckConstructionModal deckId={1} isOpen={true} onClose={vi.fn()} />
        </QueryClientProvider>
      );
    });

    const addButton = (await screen.findAllByRole("button", { name: "+ Add" }))[0];
    fireEvent.click(addButton);
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Unable to update the deck. A server error occurred."
    );
    await waitFor(() => expect(document.body.textContent).toContain("Your deck is empty."));
    expect(document.body.textContent).toContain("0 total");

    fireEvent.click(screen.getByRole("button", { name: "Dismiss deck update error" }));
    await waitFor(() => expect(screen.queryByRole("alert")).not.toBeInTheDocument());
  });

  it.each(["empty", "leader only", "base only", "Twin Suns missing leader"])(
    "does not restrict initial aspect selection for %s",
    async (setup) => {
      const leader = { ...mockDeckCards[0], aspects: [{ name: "Command", count: 1 }] };
      const base = {
        ...mockDeckCards[0],
        printingId: 70,
        cardType: "Base",
        cardName: "Command Base",
        aspects: [{ name: "Command", count: 1 }],
      };
      persistedDeckCards =
        setup === "empty"
          ? []
          : setup === "leader only"
            ? [leader]
            : setup === "base only"
              ? [base]
              : [leader, base];
      if (setup.startsWith("Twin Suns")) deckDetails.format = "Twin Suns";
      const client = createClient();
      await act(async () => {
        root.render(
          <QueryClientProvider client={client}>
            <DeckConstructionModal deckId={1} isOpen={true} onClose={vi.fn()} />
          </QueryClientProvider>
        );
      });
      await screen.findByText("Surprise Strike");
      expect(screen.getByRole("checkbox", { name: "In-aspect only" })).toBeDisabled();
      expect(screen.getByRole("checkbox", { name: "In-aspect only" })).not.toBeChecked();
      const requests = vi
        .mocked(api.get)
        .mock.calls.map(([url]) => String(url))
        .filter((url) => url.startsWith("cards/printings"));
      expect(requests.length).toBeGreaterThan(0);
      expect(requests.every((url) => !url.includes("availableAspects"))).toBe(true);
    }
  );

  it("uses server-filtered totals and resets pagination when the aspect budget changes", async () => {
    persistedDeckCards = [
      {
        ...mockDeckCards[0],
        aspects: [{ name: "Command", count: 1 }],
      },
    ];
    const previousGet = vi.mocked(api.get).getMockImplementation()!;
    vi.mocked(api.get).mockImplementation((url, config) => {
      if (!String(url).startsWith("cards/printings")) return previousGet(url, config);
      const params = new URL(String(url), "http://localhost/").searchParams;
      const restricted = params.has("availableAspects");
      const total = restricted ? 25 : 45;
      const page = Number(params.get("page") ?? 1);
      const pageSize = Number(params.get("pageSize") ?? 20);
      const items = Array.from({ length: total }, (_, index) => ({
        ...mockPrintings[0],
        printingId: 100 + index,
        cardId: 100 + index,
        cardName: `Pool card ${index + 1}`,
        aspects: [{ name: "Command", count: 1 }],
      })).slice((page - 1) * pageSize, page * pageSize);
      return Promise.resolve({ data: items, headers: { "x-total-count": String(total) } } as never);
    });
    const client = createClient();
    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <DeckConstructionModal deckId={1} isOpen={true} onClose={vi.fn()} />
        </QueryClientProvider>
      );
    });
    await screen.findByText("Showing 1–20 of 45 cards");
    fireEvent.click(screen.getByRole("button", { name: "Next page" }));
    await screen.findByText("Showing 21–40 of 45 cards");

    persistedDeckCards = [
      ...persistedDeckCards,
      {
        ...mockDeckCards[0],
        printingId: 70,
        cardName: "Command Base",
        cardType: "Base",
        aspects: [{ name: "Command", count: 1 }],
      },
    ];
    await act(async () => {
      await client.invalidateQueries();
    });
    await screen.findByText("Showing 1–20 of 25 cards");
    expect(screen.getByRole("checkbox", { name: "In-aspect only" })).toBeChecked();
    expect(screen.getByRole("combobox", { name: "Select page" })).toHaveTextContent("Page 1 of 2");
    expect(screen.getAllByRole("button", { name: "+ Add" })).toHaveLength(20);
    const requests = vi
      .mocked(api.get)
      .mock.calls.map(([url]) => new URL(String(url), "http://localhost/"))
      .filter(
        (url) =>
          url.pathname.includes("cards/printings") && url.searchParams.has("availableAspects")
      );
    expect(requests[0].searchParams.get("availableAspects")).toBe("Command,Command");
    expect(requests[0].searchParams.get("page")).toBe("1");

    fireEvent.click(screen.getByRole("button", { name: "Next page" }));
    await screen.findByText("Showing 21–25 of 25 cards");
    expect(screen.getAllByRole("button", { name: "+ Add" })).toHaveLength(5);
    fireEvent.click(screen.getByRole("checkbox", { name: "In-aspect only" }));
    await screen.findByText("Showing 1–20 of 45 cards");
    expect(screen.getByRole("checkbox", { name: "In-aspect only" })).not.toBeChecked();
  });
});
