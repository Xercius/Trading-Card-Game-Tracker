import { act } from "react";
import { createRoot } from "react-dom/client";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { fireEvent, waitFor } from "@testing-library/react";
import { CollectionSetsTable } from "../components/CollectionSetsTable";

const mockSummary = {
  sets: [
    {
      setId: 10,
      setCode: "SOR",
      setName: "Spark of Rebellion",
      releaseYear: 2024,
      releasedAt: "2024-03-08",
      isVirtualSet: false,
      totalCards: 252,
      ownedCards: 200,
      completionPercentage: 79.4,
      subsets: [
        {
          treatmentId: 1,
          treatmentName: "Standard",
          totalCards: 252,
          ownedCards: 200,
          completionPercentage: 79.4,
        },
        {
          treatmentId: 2,
          treatmentName: "Hyperspace",
          totalCards: 252,
          ownedCards: 50,
          completionPercentage: 19.8,
        },
      ],
    },
    {
      setId: 20,
      setCode: "SHD",
      setName: "Shadows of the Galaxy",
      releaseYear: 2024,
      releasedAt: "2024-07-12",
      isVirtualSet: false,
      totalCards: 262,
      ownedCards: 131,
      completionPercentage: 50.0,
      subsets: [],
    },
  ],
  totalUniqueCardsOwned: 331,
  totalUniqueCardsInGame: 514,
  overallCompletionPercentage: 64.4,
};

vi.mock("../api", () => ({
  useCollectionSetsSummaryQuery: vi.fn(() => ({
    data: mockSummary,
    isLoading: false,
    error: null,
  })),
}));

vi.mock("@/state/useGame", () => ({
  useGame: () => ({
    activeGame: "Star Wars Unlimited",
    setActiveGame: vi.fn(),
    games: ["Star Wars Unlimited"],
    isLoadingGames: false,
  }),
}));

describe("CollectionSetsTable", () => {
  it("renders sets with code, year, unique cards, and completion percentage", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CollectionSetsTable /> }]);

    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    expect(container.textContent).toContain("Spark of Rebellion");
    expect(container.textContent).toContain("SOR");
    expect(container.textContent).toContain("2024");
    expect(container.textContent).toContain("200");
    expect(container.textContent).toContain("79.4%");

    expect(container.textContent).toContain("Shadows of the Galaxy");
    expect(container.textContent).toContain("SHD");
    expect(container.textContent).toContain("50.0%");

    await act(() => {
      root.unmount();
    });
    container.remove();
  });

  it("filters sets based on search input", async () => {
    const router = createMemoryRouter([{ path: "/", element: <CollectionSetsTable /> }]);

    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    const input = container.querySelector<HTMLInputElement>("input[placeholder*='Filter sets']");
    expect(input).not.toBeNull();

    fireEvent.change(input!, { target: { value: "Shadows" } });

    await waitFor(() => {
      expect(container.textContent).toContain("Shadows of the Galaxy");
      expect(container.textContent).not.toContain("Spark of Rebellion");
    });

    await act(() => {
      root.unmount();
    });
    container.remove();
  });
});

