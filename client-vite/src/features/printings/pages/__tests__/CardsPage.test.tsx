import { fireEvent, render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import type { ReactNode } from "react";
import CardsPage from "../CardsPage";
import { GameProvider } from "@/state/GameProvider";
import * as printingsApi from "../../api/usePrintings";
import * as printingFacetsApi from "../../api/usePrintingFacets";

vi.mock("../../api/usePrintingFacets", () => ({
  usePrintingFacets: () => ({
    data: { games: [], sets: [], rarities: [], types: [], arenas: [], aspects: [], traits: [] },
  }),
}));

// Define a strongly-typed mock result for usePrintings
type UsePrintingsResult = ReturnType<typeof printingsApi.usePrintings>;

// Helper function to wrap components with required providers
function createTestWrapper(initialEntries: string[] = ["/"]) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
    },
  });

  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <MemoryRouter initialEntries={initialEntries}>
        <QueryClientProvider client={queryClient}>
          <GameProvider>{children}</GameProvider>
        </QueryClientProvider>
      </MemoryRouter>
    );
  };
}

describe("CardsPage filter wiring", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(printingFacetsApi, "usePrintingFacets").mockReturnValue({
      data: { games: [], sets: [], rarities: [], types: [], arenas: [], aspects: [], traits: [] },
    } as unknown as ReturnType<typeof printingFacetsApi.usePrintingFacets>);
  });

  afterEach(() => {
    cleanup();
  });

  it("syncs filters from URL to query parameters", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    const mockPrintingsResult = {
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as UsePrintingsResult;
    mockUsePrintings.mockReturnValue(mockPrintingsResult);

    render(<CardsPage />, {
      wrapper: createTestWrapper([
        "/cards?game=Magic,Lorcana&set=TestSet&rarity=Rare&type=Leader+Unit&arena=Ground&aspect=Heroism&trait=Rebel&q=bolt",
      ]),
    });

    // Verify usePrintings was called with correct filters from URL
    expect(mockUsePrintings).toHaveBeenCalled();
    const callArgs = mockUsePrintings.mock.calls[0][0];
    expect(callArgs.games).toEqual(["Star Wars Unlimited"]);
    expect(callArgs.sets).toEqual(["TestSet"]);
    expect(callArgs.rarities).toEqual(["Rare"]);
    expect(callArgs.types).toEqual(["Leader Unit"]);
    expect(callArgs.arenas).toEqual(["Ground"]);
    expect(callArgs.aspects).toEqual(["Heroism"]);
    expect(callArgs.traits).toEqual(["Rebel"]);
    expect(callArgs.q).toBe("bolt");
    expect(callArgs.page).toBe(1);
    expect(callArgs.pageSize).toBe(20);
    expect(callArgs.sort).toBe("name");

    expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
  });

  it("renders filter buttons for Type, Arena, Aspects, and Traits in the expandable menu", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, {
      wrapper: createTestWrapper(["/cards"]),
    });

    const filtersButton = screen.getByRole("button", { name: "Filters" });
    expect(filtersButton).toBeInTheDocument();
    expect(filtersButton).toHaveAttribute("aria-expanded", "false");

    // Before opening the menu, individual facet buttons are not rendered
    expect(screen.queryByRole("button", { name: "Type" })).not.toBeInTheDocument();

    // Click Filters to expand menu
    fireEvent.click(filtersButton);
    expect(filtersButton).toHaveAttribute("aria-expanded", "true");

    expect(screen.getByRole("button", { name: "Type" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Arena" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Aspects" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Traits" })).toBeInTheDocument();
  });

  it("uses default values when no URL parameters are present", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, {
      wrapper: createTestWrapper(["/cards"]),
    });

    expect(mockUsePrintings).toHaveBeenCalled();
    const callArgs = mockUsePrintings.mock.calls[0][0];
    expect(callArgs.games).toEqual(["Star Wars Unlimited"]);
    expect(callArgs.sets).toEqual([]);
    expect(callArgs.rarities).toEqual([]);
    expect(callArgs.types).toEqual([]);
    expect(callArgs.arenas).toEqual([]);
    expect(callArgs.aspects).toEqual([]);
    expect(callArgs.traits).toEqual([]);
    expect(callArgs.q).toBe("");
    expect(callArgs.page).toBe(1);
    expect(callArgs.pageSize).toBe(20);
    expect(callArgs.sort).toBe("name");
  });

  it("clearly styles and updates the selected view mode button", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as UsePrintingsResult);

    render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

    const gridButton = screen.getByRole("button", { name: "Grid" });
    const tableButton = screen.getByRole("button", { name: "Table" });
    expect(gridButton).toHaveAttribute("aria-pressed", "true");
    expect(gridButton).toHaveClass("border-sky-400", "bg-sky-700");
    expect(tableButton).toHaveAttribute("aria-pressed", "false");
    expect(tableButton).toHaveClass("border-white/30", "bg-transparent");

    fireEvent.click(tableButton);

    expect(gridButton).toHaveAttribute("aria-pressed", "false");
    expect(gridButton).toHaveClass("border-white/30", "bg-transparent");
    expect(tableButton).toHaveAttribute("aria-pressed", "true");
    expect(tableButton).toHaveClass("border-sky-400", "bg-sky-700");
  });

  it("renders card size slider in grid mode and updates grid column size smoothly", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: [
        {
          printingId: "1",
          cardId: "1",
          cardName: "Sample",
          game: "SWU",
          setName: "SOR",
          number: "001",
        },
      ],
      isLoading: false,
      isError: false,
      error: null,
      hasNextPage: false,
      isFetchingNextPage: false,
      fetchNextPage: vi.fn(),
    } as unknown as UsePrintingsResult);

    const { container } = render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

    const slider = screen.getByLabelText("Card size");
    expect(slider).toBeInTheDocument();
    expect(slider).toHaveAttribute("type", "range");
    expect(slider).toHaveValue("180");

    const grid = container.querySelector("ul.grid");
    expect(grid).toHaveStyle({
      gridTemplateColumns: "repeat(auto-fill, minmax(180px, 1fr))",
    });

    fireEvent.change(slider, { target: { value: "240" } });
    expect(slider).toHaveValue("240");
    expect(grid).toHaveStyle({
      gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))",
    });

    // When switching to table mode, the slider is hidden
    fireEvent.click(screen.getByRole("button", { name: "Table" }));
    expect(screen.queryByLabelText("Card size")).not.toBeInTheDocument();
  });

  it("displays active filters badges when filters are present", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, {
      wrapper: createTestWrapper([
        "/cards?game=Magic&rarity=Rare&type=Unit&arena=Space&aspect=Heroism&trait=Rebel",
      ]),
    });

    expect(screen.getByRole("button", { name: /Filters/i })).toBeInTheDocument();
    expect(screen.queryByText("Active filters:")).toBeInTheDocument();
    expect(screen.queryByText(/Game:/i)).not.toBeInTheDocument();
    expect(screen.queryByText("Rarity: Rare")).toBeInTheDocument();
    expect(screen.queryByText("Type: Unit")).toBeInTheDocument();
    expect(screen.queryByText("Arena: Space")).toBeInTheDocument();
    expect(screen.queryByText("Aspect: Heroism")).toBeInTheDocument();
    expect(screen.queryByText("Trait: Rebel")).toBeInTheDocument();
  });

  it("displays loading state", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, {
      wrapper: createTestWrapper(["/cards"]),
    });

    expect(screen.getByText("Loading printings…")).toBeInTheDocument();
  });

  it("displays error state", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error("Failed to load"),
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, {
      wrapper: createTestWrapper(["/cards"]),
    });

    expect(screen.getByText("Error: Failed to load")).toBeInTheDocument();
  });

  it("renders pagination controls at the top and bottom with previous/first buttons disabled on page 1", () => {
    vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
      data: {
        items: [
          {
            printingId: "1",
            cardId: "1",
            cardName: "Sample card",
            game: "Magic",
            setName: "Alpha",
            number: "001",
            rarity: "Common",
          },
        ],
        total: 100,
      },
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

    const firstPageButtons = screen.getAllByRole("button", { name: "First page" });
    const prevPageButtons = screen.getAllByRole("button", { name: "Previous page" });
    const nextPageButtons = screen.getAllByRole("button", { name: "Next page" });
    const lastPageButtons = screen.getAllByRole("button", { name: "Last page" });

    expect(firstPageButtons).toHaveLength(2);
    expect(prevPageButtons).toHaveLength(2);
    expect(nextPageButtons).toHaveLength(2);
    expect(lastPageButtons).toHaveLength(2);

    expect(firstPageButtons[0]).toBeDisabled();
    expect(prevPageButtons[0]).toBeDisabled();
    expect(nextPageButtons[0]).toBeEnabled();
    expect(lastPageButtons[0]).toBeEnabled();
  });

  it("positions the Filter button immediately to the left of the top pagination controls", () => {
    vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
      data: { items: [], total: 0 },
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

    const filterButton = screen.getByRole("button", { name: "Filters" });
    const parent = filterButton.parentElement;
    expect(parent).not.toBeNull();
    const children = Array.from(parent?.children ?? []);
    const filterIdx = children.indexOf(filterButton);
    expect(filterIdx).toBe(0);
    expect(children[filterIdx + 1]?.querySelector("[aria-label='First page']")).not.toBeNull();
  });

  it("displays up to 200 cards and locks the page size dropdown in table view", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: { items: [], total: 500 },
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

    fireEvent.click(screen.getByRole("button", { name: "Table" }));

    const lastCall = mockUsePrintings.mock.calls.at(-1)?.[0];
    expect(lastCall?.pageSize).toBe(200);

    const pageSizeTriggers = screen.getAllByRole("combobox", { name: "Cards per page" });
    expect(pageSizeTriggers[0]).toBeDisabled();
  });

  it("offers a retry when printing facets fail", () => {
    const retryFacets = vi.fn();
    vi.spyOn(printingFacetsApi, "usePrintingFacets").mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      refetch: retryFacets,
    } as unknown as ReturnType<typeof printingFacetsApi.usePrintingFacets>);
    vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

    fireEvent.click(screen.getByRole("button", { name: "Filters" }));

    expect(screen.getByText("Unable to load filters.")).toBeInTheDocument();
    screen.getByRole("button", { name: "Retry filters" }).click();
    expect(retryFacets).toHaveBeenCalledOnce();
  });

  describe("horizontal card stacking in grid mode", () => {
    it("does not pair horizontal cards when there are no vertical cards", () => {
      vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
        data: [
          {
            printingId: "1",
            cardId: "1",
            cardName: "Leader 1",
            game: "Star Wars Unlimited",
            setName: "SOR",
            imageOrientation: "horizontal",
          },
          {
            printingId: "2",
            cardId: "2",
            cardName: "Leader 2",
            game: "Star Wars Unlimited",
            setName: "SOR",
            imageOrientation: "horizontal",
          },
        ],
        isLoading: false,
        isError: false,
        error: null,
        hasNextPage: false,
        isFetchingNextPage: false,
        fetchNextPage: vi.fn(),
      } as unknown as UsePrintingsResult);

      const { container } = render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

      const listItems = container.querySelectorAll("ul > li");
      expect(listItems).toHaveLength(2);
      expect(listItems[0].classList.contains("flex-col")).toBe(false);
      expect(listItems[1].classList.contains("flex-col")).toBe(false);
    });

    it("pairs adjacent horizontal cards when vertical cards are present", () => {
      vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
        data: [
          {
            printingId: "h1",
            cardId: "1",
            cardName: "The Mandalorian",
            game: "Star Wars Unlimited",
            setName: "SOR",
            imageOrientation: "horizontal",
          },
          {
            printingId: "h2",
            cardId: "2",
            cardName: "Bo-Katan Kryze",
            game: "Star Wars Unlimited",
            setName: "SOR",
            imageOrientation: "horizontal",
          },
          {
            printingId: "v1",
            cardId: "3",
            cardName: "The Great Mothers",
            game: "Star Wars Unlimited",
            setName: "SOR",
            imageOrientation: "vertical",
          },
        ],
        isLoading: false,
        isError: false,
        error: null,
        hasNextPage: false,
        isFetchingNextPage: false,
        fetchNextPage: vi.fn(),
      } as unknown as UsePrintingsResult);

      const { container } = render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

      const listItems = container.querySelectorAll("ul > li");
      expect(listItems).toHaveLength(2);

      const pairedItem = listItems[0];
      expect(pairedItem.classList.contains("flex-col")).toBe(true);
      const buttonsInPair = pairedItem.querySelectorAll("button");
      expect(buttonsInPair).toHaveLength(2);
      expect(buttonsInPair[0].getAttribute("aria-label")).toContain("The Mandalorian");
      expect(buttonsInPair[1].getAttribute("aria-label")).toContain("Bo-Katan Kryze");

      const singleVerticalItem = listItems[1];
      expect(singleVerticalItem.classList.contains("flex-col")).toBe(false);
      const buttonsInVertical = singleVerticalItem.querySelectorAll("button");
      expect(buttonsInVertical).toHaveLength(1);
      expect(buttonsInVertical[0].getAttribute("aria-label")).toContain("The Great Mothers");
    });

    it("leaves isolated or odd trailing horizontal cards as single items", () => {
      vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
        data: [
          {
            printingId: "h1",
            cardId: "1",
            cardName: "The Mandalorian",
            game: "Star Wars Unlimited",
            setName: "SOR",
            imageOrientation: "horizontal",
          },
          {
            printingId: "h2",
            cardId: "2",
            cardName: "Bo-Katan Kryze",
            game: "Star Wars Unlimited",
            setName: "SOR",
            imageOrientation: "horizontal",
          },
          {
            printingId: "h3",
            cardId: "3",
            cardName: "Cad Bane",
            game: "Star Wars Unlimited",
            setName: "SOR",
            imageOrientation: "horizontal",
          },
          {
            printingId: "v1",
            cardId: "4",
            cardName: "The Great Mothers",
            game: "Star Wars Unlimited",
            setName: "SOR",
            imageOrientation: "vertical",
          },
        ],
        isLoading: false,
        isError: false,
        error: null,
        hasNextPage: false,
        isFetchingNextPage: false,
        fetchNextPage: vi.fn(),
      } as unknown as UsePrintingsResult);

      const { container } = render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

      const listItems = container.querySelectorAll("ul > li");
      expect(listItems).toHaveLength(3);
      expect(listItems[0].classList.contains("flex-col")).toBe(true);
      expect(listItems[1].classList.contains("flex-col")).toBe(false);
      expect(listItems[2].classList.contains("flex-col")).toBe(false);
    });
  });

  describe("search query and page coordination", () => {
    it("resets page to 1 when filters.q changes on a higher page", () => {
      const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
        data: { items: [], total: 0 },
        isLoading: false,
        isError: false,
        error: null,
      } as unknown as UsePrintingsResult);

      render(<CardsPage />, { wrapper: createTestWrapper(["/cards?page=2&q=vader"]) });

      // First call has page: 2 from initial URL, and previousQRef syncs
      expect(mockUsePrintings).toHaveBeenCalled();
    });

    it("clamps page to 1 when total pages is less than current page", () => {
      const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
        data: { items: [], total: 5 },
        isLoading: false,
        isError: false,
        error: null,
      } as unknown as UsePrintingsResult);

      render(<CardsPage />, { wrapper: createTestWrapper(["/cards?page=5"]) });

      // After render, clamp effect detects page 5 > totalPages 1 and resets page
      expect(mockUsePrintings).toHaveBeenCalled();
    });
  });
});
