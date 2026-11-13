import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, fireEvent, waitFor } from "@testing-library/react";
import CollectionPage from "../CollectionPage";

const mockSummary = {
  sets: [
    {
      setId: 1,
      setCode: "SOR",
      setName: "Spark of Rebellion",
      releaseYear: 2024,
      releasedAt: "2024-03-08",
      isVirtualSet: false,
      totalCards: 252,
      ownedCards: 180,
      completionPercentage: 71.4,
      subsets: [
        {
          treatmentId: 1,
          treatmentName: "Standard",
          totalCards: 252,
          ownedCards: 180,
          completionPercentage: 71.4,
        },
        {
          treatmentId: 2,
          treatmentName: "Hyperspace",
          totalCards: 252,
          ownedCards: 30,
          completionPercentage: 11.9,
        },
      ],
    },
  ],
  totalUniqueCardsOwned: 180,
  totalUniqueCardsInGame: 252,
  overallCompletionPercentage: 71.4,
};

vi.mock("../api", () => ({
  collectionKeys: { all: ["collection"], list: vi.fn(() => ["collection", { userId: 1 }]) },
  useCollectionSetsSummaryQuery: vi.fn(() => ({
    data: mockSummary,
    isLoading: false,
    error: null,
  })),
  useBatchSetCollectionMutation: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
}));

vi.mock("@/state/useUser", () => ({
  useUser: () => ({ userId: 1 }),
}));

vi.mock("@/state/useGame", () => ({
  useGame: () => ({
    activeGame: "Star Wars Unlimited",
    setActiveGame: vi.fn(),
    games: ["Star Wars Unlimited"],
    isLoadingGames: false,
  }),
}));

vi.mock("../useCollectionValueHistory", () => ({
  useCollectionValueHistory: vi.fn(() => ({
    data: [
      { d: "2024-01-01", v: 2 },
      { d: "2024-01-02", v: 4 },
    ],
    isLoading: false,
    isError: false,
  })),
}));

describe("CollectionPage", () => {
  function renderCollectionPage() {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const router = createMemoryRouter([{ path: "/", element: <CollectionPage /> }]);
    return render(
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    );
  }

  it("renders collection value and sets overview table", async () => {
    const { container } = renderCollectionPage();

    await waitFor(() => {
      expect(container.textContent).toContain("Proxies excluded");
    });
    expect(container.textContent).toContain("Sets & Expansions");
    expect(container.textContent).toContain("Spark of Rebellion");
    expect(container.textContent).toContain("SOR");
    expect(container.textContent).toContain("71.4%");
  });

  it("expands subsets when clicking expand toggle", async () => {
    const { container } = renderCollectionPage();

    await waitFor(() => {
      expect(container.textContent).toContain("Spark of Rebellion");
    });
    expect(container.textContent).not.toContain("Hyperspace");

    // Click expand subsets chevron
    const expandBtn = container.querySelector('button[aria-label="Expand subsets"]');
    expect(expandBtn).not.toBeNull();
    fireEvent.click(expandBtn!);

    await waitFor(() => {
      expect(container.textContent).toContain("Standard");
      expect(container.textContent).toContain("Hyperspace");
      expect(container.textContent).toContain("11.9%");
    });
  });
});
