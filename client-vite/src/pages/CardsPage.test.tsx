import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/components/VirtualizedCardGrid", () => ({
  default: () => <div data-testid="grid" />,
}));

vi.mock("@/features/cards/filters/FiltersRail", () => ({
  default: () => <div data-testid="filters-rail" />,
}));

vi.mock("@/features/cards/filters/PillsBar", () => ({
  default: () => <div data-testid="pills-bar" />,
}));

vi.mock("@/state/useUser", () => ({
  useUser: () => ({ userId: 1 }),
}));

const querySpy = vi.fn(() => ({
  data: undefined,
  isError: false,
  isFetching: false,
  isFetchingNextPage: false,
  hasNextPage: false,
  fetchNextPage: vi.fn(),
}));

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
    querySpy.mockClear();
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
});
