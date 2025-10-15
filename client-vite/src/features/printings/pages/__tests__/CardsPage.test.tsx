import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import CardsPage from "../CardsPage";
import * as printingsApi from "../../api/usePrintings";

describe("CardsPage filter wiring", () => {
  let queryClient: QueryClient;
  let container: HTMLDivElement;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false, staleTime: Infinity },
      },
    });
    container = document.createElement("div");
    document.body.appendChild(container);
    vi.clearAllMocks();
  });

  afterEach(() => {
    document.body.removeChild(container);
  });

  it("syncs filters from URL to query parameters", async () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as any);

    const root = createRoot(container);
    
    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/cards?game=Magic,Lorcana&set=TestSet&rarity=Rare&q=bolt"]}>
          <QueryClientProvider client={queryClient}>
            <CardsPage />
          </QueryClientProvider>
        </MemoryRouter>
      );
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
    const searchInput = container.querySelector('input[type="search"]') as HTMLInputElement;
    expect(searchInput?.value).toBe("bolt");

    await act(async () => {
      root.unmount();
    });
  });

  it("uses default values when no URL parameters are present", async () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as any);

    const root = createRoot(container);
    
    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/cards"]}>
          <QueryClientProvider client={queryClient}>
            <CardsPage />
          </QueryClientProvider>
        </MemoryRouter>
      );
    });

    expect(mockUsePrintings).toHaveBeenCalled();
    const callArgs = mockUsePrintings.mock.calls[0][0];
    expect(callArgs.games).toEqual([]);
    expect(callArgs.sets).toEqual([]);
    expect(callArgs.rarities).toEqual([]);
    expect(callArgs.q).toBe("");
    expect(callArgs.page).toBe(1);
    expect(callArgs.pageSize).toBe(60);

    await act(async () => {
      root.unmount();
    });
  });

  it("displays active filters badges when filters are present", async () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as any);

    const root = createRoot(container);
    
    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/cards?game=Magic&rarity=Rare"]}>
          <QueryClientProvider client={queryClient}>
            <CardsPage />
          </QueryClientProvider>
        </MemoryRouter>
      );
    });

    const activeFiltersText = container.textContent;
    expect(activeFiltersText).toContain("Active filters:");
    expect(activeFiltersText).toContain("Game: Magic");
    expect(activeFiltersText).toContain("Rarity: Rare");

    await act(async () => {
      root.unmount();
    });
  });

  it("displays loading state", async () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
    } as any);

    const root = createRoot(container);
    
    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/cards"]}>
          <QueryClientProvider client={queryClient}>
            <CardsPage />
          </QueryClientProvider>
        </MemoryRouter>
      );
    });

    expect(container.textContent).toContain("Loading printings");

    await act(async () => {
      root.unmount();
    });
  });

  it("displays error state", async () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error("Failed to load"),
    } as any);

    const root = createRoot(container);
    
    await act(async () => {
      root.render(
        <MemoryRouter initialEntries={["/cards"]}>
          <QueryClientProvider client={queryClient}>
            <CardsPage />
          </QueryClientProvider>
        </MemoryRouter>
      );
    });

    expect(container.textContent).toContain("Error: Failed to load");

    await act(async () => {
      root.unmount();
    });
  });
});
