import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/components/VirtualizedCardGrid", () => ({
  default: ({ items, onCardClick }: { items: Array<{ id: number | string }>; onCardClick?: (card: unknown) => void }) => (
    <div data-testid="grid">
      {items.map((item) => (
        <button
          key={item.id}
          type="button"
          data-testid={`card-${item.id}`}
          onClick={() => onCardClick?.(item)}
        >
          Card {item.id}
        </button>
      ))}
    </div>
  ),
}));

vi.mock("@/features/cards/filters/FiltersRail", () => ({
  default: () => <div data-testid="filters-rail" />,
}));

vi.mock("@/features/cards/filters/PillsBar", () => ({
  default: () => <div data-testid="pills-bar" />,
}));

vi.mock("@/features/cards/components/CardModal", () => ({
  default: ({ open, cardId }: { open: boolean; cardId: number }) =>
    open ? <div data-testid="card-modal">Card {cardId}</div> : null,
}));

vi.mock("@/state/useUser", () => ({
  useUser: () => ({ userId: 1 }),
}));

type QueryResult = {
  data: unknown;
  isError: boolean;
  isFetching: boolean;
  isFetchingNextPage: boolean;
  hasNextPage: boolean;
  fetchNextPage: () => void;
};

const createQueryResult = (overrides: Partial<QueryResult> = {}): QueryResult => ({
  data: undefined,
  isError: false,
  isFetching: false,
  isFetchingNextPage: false,
  hasNextPage: false,
  fetchNextPage: vi.fn(),
  ...overrides,
});

const querySpy = vi.fn(() => createQueryResult());

vi.mock("@tanstack/react-query", async () => {
  const actual = await vi.importActual<Record<string, unknown>>("@tanstack/react-query");
  return {
    ...actual,
    useInfiniteQuery: (options: unknown) => querySpy(options),
  };
});

import CardsPage from "./CardsPage";

describe("CardsPage", () => {
  beforeEach(() => {
    querySpy.mockReset();
    querySpy.mockImplementation(() => createQueryResult());
  });

  it("includes filters in the query key", async () => {
    const router = createMemoryRouter([
      { path: "/", element: <CardsPage /> },
    ], {
      initialEntries: ["/?game=Magic"],
    });

    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    expect(querySpy).toHaveBeenCalled();
    const initialOptions = querySpy.mock.calls[0]?.[0] as { queryKey: unknown };
    expect(initialOptions?.queryKey).toEqual([
      "cards",
      { userId: 1, filters: ["", "Magic", "", ""] },
    ]);

    const initialCallCount = querySpy.mock.calls.length;

    await act(async () => {
      await router.navigate("/?game=Magic&rarity=Rare");
    });

    expect(querySpy.mock.calls.length).toBeGreaterThan(initialCallCount);
    const latestOptions = querySpy.mock.calls.at(-1)?.[0] as { queryKey: unknown };
    expect(latestOptions?.queryKey).toEqual([
      "cards",
      { userId: 1, filters: ["", "Magic", "", "Rare"] },
    ]);
    expect(latestOptions?.queryKey).not.toBe(initialOptions?.queryKey);

    await act(async () => {
      root.unmount();
    });
  });

  it("opens the card modal when a card is clicked", async () => {
    const card = {
      id: 7,
      primaryPrintingId: 42,
      name: "Test Card",
      game: "Test",
    };

    querySpy.mockImplementation(() =>
      createQueryResult({
        data: {
          pages: [
            {
              items: [card],
              nextSkip: null,
            },
          ],
        },
      })
    );

    const router = createMemoryRouter([
      { path: "/", element: <CardsPage /> },
    ]);

    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    const cardButton = container.querySelector<HTMLButtonElement>("[data-testid='card-7']");
    expect(cardButton).not.toBeNull();

    await act(async () => {
      cardButton?.click();
    });

    expect(container.querySelector("[data-testid='card-modal']")).not.toBeNull();

    await act(async () => {
      root.unmount();
    });
  });
});
