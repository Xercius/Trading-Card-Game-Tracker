import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { beforeEach, describe, expect, it, vi, afterEach } from "vitest";
import { createMemoryRouter, RouterProvider } from "react-router-dom";

const usePrintingsMock = vi.fn();

vi.mock("../api/usePrintings", () => ({
  usePrintings: (query: unknown) => usePrintingsMock(query),
}));

import CardsPage from "./CardsPage";

describe("CardsPage", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    usePrintingsMock.mockReset();
    usePrintingsMock.mockImplementation(() => ({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    }));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("calls usePrintings with the default query", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    expect(usePrintingsMock).toHaveBeenCalled();
    expect(usePrintingsMock.mock.calls[0]?.[0]).toEqual({
      q: "",
      games: [],
      sets: [],
      rarities: [],
      page: 1,
      pageSize: 60,
      sort: undefined,
    });

    await act(async () => {
      root.unmount();
    });
  });

  it("renders FiltersRail with search input", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    usePrintingsMock.mockImplementation(() => ({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    }));

    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    // FiltersRail should be present in desktop view
    const filtersRail = container.querySelector(".md\\:w-80");
    expect(filtersRail).not.toBeNull();

    // Search input should be in FiltersRail component
    const input = container.querySelector<HTMLInputElement>("input[placeholder*='Search']");
    expect(input).not.toBeNull();

    await act(async () => {
      root.unmount();
    });
  });

  it("renders printing tiles", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    usePrintingsMock.mockImplementation(() => ({
      data: [
        {
          printingId: "p1",
          cardId: "c1",
          cardName: "Sample Card",
          game: "Game A",
          setName: "Set A",
          setCode: null,
          number: "001",
          rarity: "Common",
          imageUrl: null,
        },
      ],
      isLoading: false,
      isError: false,
      error: null,
    }));

    const container = document.createElement("div");
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    expect(container.textContent).toContain("Sample Card");
    expect(container.textContent).toContain("Game A • Set A #001 • Common");

    await act(async () => {
      root.unmount();
    });
  });
});
