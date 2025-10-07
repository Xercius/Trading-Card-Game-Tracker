import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import CollectionPage from "../CollectionPage";

const mockData = {
  data: {
    items: [
      {
        cardPrintingId: 42,
        quantityOwned: 3,
        quantityWanted: 0,
        quantityProxyOwned: 2,
        availability: 3,
        availabilityWithProxies: 5,
        cardId: 99,
        cardName: "Sample Card",
        game: "Test",
        set: "Alpha",
        number: "001",
        rarity: "Rare",
        style: "",
        imageUrl: null,
      },
    ],
    total: 1,
    page: 1,
    pageSize: 50,
  },
  isPending: false,
  isError: false,
  refetch: async () => {},
  isFetching: false,
};

vi.mock("../api", () => ({
  collectionKeys: { list: vi.fn(() => ["collection", { userId: 1 }]) },
  useCollectionQuery: vi.fn(() => mockData),
}));

vi.mock("@/state/useUser", () => ({
  useUser: () => ({ userId: 1 }),
}));

describe("CollectionPage", () => {
  it("toggles availability label when include proxies changes", async () => {
    const router = createMemoryRouter([
      { path: "/", element: <CollectionPage /> },
    ]);

    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<RouterProvider router={router} />);
    });

    const badgeLabel = () => container.querySelector<HTMLSpanElement>("span.font-semibold");
    expect(badgeLabel()?.textContent).toBe("A");

    const checkbox = container.querySelector<HTMLInputElement>("input[type='checkbox']");
    expect(checkbox).not.toBeNull();

    await act(async () => {
      checkbox!.checked = true;
      checkbox!.dispatchEvent(new Event("change", { bubbles: true }));
      await Promise.resolve();
    });

    expect(badgeLabel()?.textContent).toBe("A+P");
    const valueSpan = badgeLabel()?.nextElementSibling;
    expect(valueSpan?.textContent).toBe("5");

    await act(() => {
      root.unmount();
    });
    container.remove();
  });
});
