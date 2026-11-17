import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { ReactNode } from "react";
import http from "@/lib/http";
import DeckBuilderPage from "../DeckBuilderPage";

vi.mock("@/state/useGame", () => ({ useGame: () => ({ activeGame: "Star Wars Unlimited" }) }));
vi.mock("@/hooks/useIncludeProxies", () => ({ useIncludeProxies: () => [false, vi.fn()] }));
vi.mock("../StickyDeckSidebar", () => ({ default: () => null }));
vi.mock("../api", () => ({
  useDeckDetails: () => ({ data: { name: "Test Deck", game: "Magic The Gathering" } }),
  useDeckCardsWithAvailability: () => ({ data: [] }),
  useDeckQuantityMutation: () => ({ mutate: vi.fn() }),
  useDeckValueHistory: () => ({ data: [] }),
}));
vi.mock("@/components/VirtualizedCardGrid", () => ({
  default: ({
    items,
    renderItem,
    fetchNextPage,
    hasNextPage,
  }: {
    items: { id: number }[];
    renderItem: (item: { id: number }) => ReactNode;
    fetchNextPage: () => void;
    hasNextPage: boolean;
  }) => (
    <>
      {items.map((item) => (
        <div key={item.id}>{renderItem(item)}</div>
      ))}
      {hasNextPage && <button onClick={fetchNextPage}>Load more</button>}
    </>
  ),
}));

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("DeckBuilderPage filters", () => {
  it("sends drawer filters, refreshes results on changes, and preserves printing IDs", async () => {
    const requests: URLSearchParams[] = [];
    vi.spyOn(http, "get").mockImplementation((url) => {
      const path = String(url);
      if (path.includes("printings")) {
        const params = new URLSearchParams(path.split("?")[1]);
        requests.push(params);
        const name = params.get("cost") === "5" ? "Updated Knight" : "White Knight";
        return Promise.resolve({
          data: [
            {
              printingId: "42",
              cardId: 7,
              cardName: name,
              game: "Magic The Gathering",
              setName: "Test Set",
            },
          ],
          headers: { "x-total-count": "1" },
        } as never);
      }
      return Promise.resolve({ data: { games: [], sets: [], rarities: [], types: [] } } as never);
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter
          initialEntries={[
            "/?cost=3&power=%3E2&colorIdentity=White&subType=Knight&toughness=4&loyalty=2&upgradePower=1&upgradeHp=1&hp=100&stage=Stage+1&specialType=Tera",
          ]}
        >
          <DeckBuilderPage deckId={1} />
        </MemoryRouter>
      </QueryClientProvider>
    );
    expect(await screen.findByRole("button", { name: /White Knight/ })).toBeInTheDocument();
    expect(Object.fromEntries(requests[0])).toMatchObject({
      game: "Magic The Gathering",
      cost: "3",
      power: ">2",
      colorIdentity: "White",
      subType: "Knight",
      toughness: "4",
      loyalty: "2",
      upgradePower: "1",
      upgradeHp: "1",
      hp: "100",
      stage: "Stage 1",
      specialType: "Tera",
      uniqueCards: "true",
    });
    const transfer = { setData: vi.fn(), effectAllowed: "" };
    fireEvent.dragStart(screen.getByRole("button", { name: /White Knight/ }).parentElement!, {
      dataTransfer: transfer,
    });
    expect(transfer.setData).toHaveBeenCalledWith("application/x-printing-id", "42");
    fireEvent.click(screen.getByRole("button", { name: "Open filters" }));
    const cost = screen.getByRole("textbox", { name: "Mana Value" });
    fireEvent.change(cost, { target: { value: "5" } });
    fireEvent.keyDown(cost, { key: "Enter" });
    await waitFor(() =>
      expect(screen.getByRole("button", { name: /Updated Knight/ })).toBeInTheDocument()
    );
    expect(requests.at(-1)?.get("cost")).toBe("5");
  });
});
