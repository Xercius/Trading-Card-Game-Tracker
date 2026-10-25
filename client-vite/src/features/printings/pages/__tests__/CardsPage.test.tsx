import { fireEvent, render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import type { ReactNode } from "react";
import CardsPage from "../CardsPage";
import * as printingsApi from "../../api/usePrintings";
import * as printingFacetsApi from "../../api/usePrintingFacets";

vi.mock("../../api/usePrintingFacets", () => ({
  usePrintingFacets: () => ({ data: { games: [], sets: [], rarities: [] } }),
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
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      </MemoryRouter>
    );
  };
}

describe("CardsPage filter wiring", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(printingFacetsApi, "usePrintingFacets").mockReturnValue({
      data: { games: [], sets: [], rarities: [] },
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
      wrapper: createTestWrapper(["/cards?game=Magic,Lorcana&set=TestSet&rarity=Rare&q=bolt"]),
    });

    // Verify usePrintings was called with correct filters from URL
    expect(mockUsePrintings).toHaveBeenCalled();
    const callArgs = mockUsePrintings.mock.calls[0][0];
    expect(callArgs.games).toEqual(["Magic", "Lorcana"]); // order preserved from URL
    expect(callArgs.sets).toEqual(["TestSet"]);
    expect(callArgs.rarities).toEqual(["Rare"]);
    expect(callArgs.q).toBe("bolt");
    expect(callArgs.page).toBe(1);
    expect(callArgs.pageSize).toBe(60);

    // Verify search input is populated
    const searchInput = screen.getByRole("searchbox") as HTMLInputElement;
    expect(searchInput.value).toBe("bolt");
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
    expect(callArgs.games).toEqual([]);
    expect(callArgs.sets).toEqual([]);
    expect(callArgs.rarities).toEqual([]);
    expect(callArgs.q).toBe("");
    expect(callArgs.page).toBe(1);
    expect(callArgs.pageSize).toBe(60);
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

  it("displays active filters badges when filters are present", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, {
      wrapper: createTestWrapper(["/cards?game=Magic&rarity=Rare"]),
    });

    expect(screen.queryByText("Active filters:")).toBeInTheDocument();
    expect(screen.queryByText("Game: Magic")).toBeInTheDocument();
    expect(screen.queryByText("Rarity: Rare")).toBeInTheDocument();
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

  it("offers a load-more action when another page is available", () => {
    vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
      hasNextPage: true,
      isFetchingNextPage: false,
      fetchNextPage: vi.fn(),
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

    expect(screen.getByRole("button", { name: "Load more printings" })).toBeInTheDocument();
  });

  it("shows loading feedback while requesting another page", () => {
    vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
      hasNextPage: true,
      isFetchingNextPage: true,
      fetchNextPage: vi.fn(),
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

    expect(screen.getByRole("button", { name: "Loading more printings…" })).toBeDisabled();
  });

  it("shows when no more printing pages remain", () => {
    vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
      data: [
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
      isLoading: false,
      isError: false,
      error: null,
      hasNextPage: false,
      isFetchingNextPage: false,
      fetchNextPage: vi.fn(),
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

    expect(screen.getByText("No more printings.")).toBeInTheDocument();
  });

  it("keeps loaded printings and offers a retry after a later page fails", () => {
    vi.spyOn(printingsApi, "usePrintings").mockReturnValue({
      data: [
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
      isLoading: false,
      isError: true,
      isFetchNextPageError: true,
      error: new Error("Failed to load next page"),
      hasNextPage: true,
      isFetchingNextPage: false,
      fetchNextPage: vi.fn(),
    } as unknown as UsePrintingsResult);

    render(<CardsPage />, { wrapper: createTestWrapper(["/cards"]) });

    expect(screen.getByText("1 printings")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Retry loading printings" })).toBeInTheDocument();
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

    expect(screen.getByText("Unable to load filters.")).toBeInTheDocument();
    screen.getByRole("button", { name: "Retry filters" }).click();
    expect(retryFacets).toHaveBeenCalledOnce();
  });
});
