import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { ReactElement } from "react";
import CardsPage from "../CardsPage";
import * as printingsApi from "../../api/usePrintings";

// Helper function to wrap components with required providers
function createTestWrapper(initialEntries: string[] = ["/"]) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
    },
  });

  return function Wrapper({ children }: { children: ReactElement }) {
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
  });

  it("syncs filters from URL to query parameters", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    const mockPrintingsResult: ReturnType<typeof printingsApi.usePrintings> = {
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    };
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
    } as any);

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

  it("displays active filters badges when filters are present", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as any);

    const { container } = render(<CardsPage />, {
      wrapper: createTestWrapper(["/cards?game=Magic&rarity=Rare"]),
    });

    const text = container.textContent;
    expect(text).toContain("Active filters:");
    expect(text).toContain("Game: Magic");
    expect(text).toContain("Rarity: Rare");
  });

  it("displays loading state", () => {
    const mockUsePrintings = vi.spyOn(printingsApi, "usePrintings");
    mockUsePrintings.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
    } as any);

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
    } as any);

    render(<CardsPage />, {
      wrapper: createTestWrapper(["/cards"]),
    });

    expect(screen.getByText("Error: Failed to load")).toBeInTheDocument();
  });
});
