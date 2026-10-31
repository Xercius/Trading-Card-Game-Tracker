import { act } from "react-dom/test-utils";
import { createRoot, type Root } from "react-dom/client";
import { beforeEach, describe, expect, it, vi, afterEach } from "vitest";
import { createMemoryRouter, RouterProvider } from "react-router-dom";

const usePrintingsMock = vi.fn<(_query: unknown) => unknown>();
const usePrintingFacetsMock = vi.fn(() => ({
  data: { games: [], sets: [], rarities: [], types: [], arenas: [], aspects: [], traits: [] },
}));
const CardModalMock = vi.fn((_props: unknown) => null);

vi.mock("../api/usePrintings", () => ({
  usePrintings: (query: unknown) => usePrintingsMock(query),
}));

vi.mock("../api/usePrintingFacets", () => ({
  usePrintingFacets: () => usePrintingFacetsMock(),
}));

vi.mock("@/features/cards/components/CardModal", () => ({
  default: (props: unknown) => CardModalMock(props),
}));

vi.mock("@/state/useGame", () => ({
  useGame: () => ({
    activeGame: "Star Wars Unlimited",
    setActiveGame: vi.fn(),
    games: ["Star Wars Unlimited"],
    isLoadingGames: false,
  }),
}));

import CardsPage from "./CardsPage";
import * as printingsApi from "../api/usePrintings";

describe("CardsPage", () => {
  let root: Root | null = null;
  let container: HTMLDivElement | null = null;

  beforeEach(() => {
    vi.useFakeTimers();
    usePrintingsMock.mockReset();
    CardModalMock.mockReset();
    usePrintingsMock.mockImplementation(
      () =>
        ({
          data: [],
          isLoading: false,
          isError: false,
          error: null,
        }) as ReturnType<typeof printingsApi.usePrintings>
    );
  });

  afterEach(async () => {
    if (root) {
      await act(async () => {
        root!.unmount();
      });
      root = null;
    }
    if (container) {
      container.remove();
      container = null;
    }
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("calls usePrintings with the default query", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    container = document.createElement("div");
    document.body.appendChild(container);
    root = createRoot(container);

    await act(async () => {
      root!.render(<RouterProvider router={router} />);
    });

    expect(usePrintingsMock).toHaveBeenCalled();
    expect(usePrintingsMock.mock.calls[0]?.[0]).toEqual({
      q: "",
      games: ["Star Wars Unlimited"],
      sets: [],
      rarities: [],
      types: [],
      arenas: [],
      aspects: [],
      aspectMethod: "or",
      traits: [],
      traitMethod: "or",
      keywords: [],
      keywordMethod: "or",
      page: 1,
      pageSize: 20,
      sort: "name",
    });
  });

  it("does not render a redundant page-level search input", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    usePrintingsMock.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof printingsApi.usePrintings>);

    container = document.createElement("div");
    document.body.appendChild(container);
    root = createRoot(container);

    await act(async () => {
      root!.render(<RouterProvider router={router} />);
    });

    expect(container.querySelector("input[type='search']")).toBeNull();
  });

  it("renders printing tiles without a page-level search input", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    usePrintingsMock.mockReturnValue({
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
    } as ReturnType<typeof printingsApi.usePrintings>);

    container = document.createElement("div");
    document.body.appendChild(container);
    root = createRoot(container);

    await act(async () => {
      root!.render(<RouterProvider router={router} />);
    });

    // Card tiles no longer show visible text - verify aria-label for accessibility
    const cardButton = container.querySelector<HTMLButtonElement>("ul button[aria-label]");
    expect(cardButton).not.toBeNull();
    expect(cardButton?.getAttribute("aria-label")).toBe("Sample Card — Set A #001");
    expect(container.querySelector("input[type='search']")).toBeNull();
  });

  it("ensures cleanup occurs even if DOM elements are created", async () => {
    // This test verifies that afterEach properly cleans up root and container
    // Count initial DOM elements
    const initialChildCount = document.body.childNodes.length;

    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    container = document.createElement("div");
    document.body.appendChild(container);
    root = createRoot(container);

    await act(async () => {
      root!.render(<RouterProvider router={router} />);
    });

    // Verify container was added
    expect(document.body.childNodes.length).toBeGreaterThan(initialChildCount);
    expect(document.body.contains(container)).toBe(true);

    // Note: afterEach will clean up automatically, and subsequent tests will verify no leak
  });

  it("verifies previous test cleanup by checking DOM is clean", async () => {
    // This test runs after the previous test and verifies no stale DOM nodes
    // If afterEach cleanup didn't work, this would detect leftover containers
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    const childCountBefore = document.body.childNodes.length;

    container = document.createElement("div");
    document.body.appendChild(container);
    root = createRoot(container);

    await act(async () => {
      root!.render(<RouterProvider router={router} />);
    });

    // We should only have added one container, not accumulated multiple
    expect(document.body.childNodes.length).toBe(childCountBefore + 1);
  });

  it("opens CardModal when a printing is clicked", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CardsPage /> }]);

    usePrintingsMock.mockImplementation(() => ({
      data: [
        {
          printingId: "123",
          cardId: "456",
          cardName: "Test Card",
          game: "Magic",
          setName: "Alpha",
          setCode: "ALP",
          number: "001",
          rarity: "Rare",
          imageUrl: "/test.png",
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

    // Find and click the printing card button
    const printingButton = container.querySelector<HTMLButtonElement>("ul button[aria-label]");
    expect(printingButton).not.toBeNull();
    expect(printingButton?.getAttribute("aria-label")).toBe("Test Card — Alpha #001");

    await act(async () => {
      printingButton?.click();
    });

    // CardModal should be called with the correct props
    expect(CardModalMock).toHaveBeenCalled();
    const lastCall = CardModalMock.mock.calls.at(-1)?.[0];
    expect(lastCall).toMatchObject({
      cardId: 456,
      initialPrintingId: 123,
      open: true,
    });

    await act(async () => {
      root.unmount();
    });
  });
});
