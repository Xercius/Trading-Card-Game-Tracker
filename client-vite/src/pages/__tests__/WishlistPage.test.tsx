import { afterEach, describe, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { createRoot } from "react-dom/client";
import { act } from "react-dom/test-utils";
import WishlistPage from "../WishlistPage";
import http from "@/lib/http";
import {
  collectionKeys,
  type CollectionItem,
  type Paged as CollectionPaged,
} from "@/features/collection/api";

vi.mock("@/state/useUser", () => ({
  useUser: () => ({
    userId: 42,
    setUserId: () => {},
    users: [],
    refreshUsers: () => Promise.resolve(),
  }),
}));

type WishlistItem = {
  cardPrintingId: number;
  quantityWanted: number;
  cardId: number;
  cardName: string;
  game: string;
  set: string;
  number: string;
  rarity: string;
  style: string;
  imageUrl: string | null;
};

type WishlistPaged = {
  items: WishlistItem[];
  total: number;
  page: number;
  pageSize: number;
};

const wishlistQueryKey = ["wishlist", 42, 1, 50, "", ""] as const;

function createClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
}

async function flush() {
  await act(async () => {
    await Promise.resolve();
  });
}

async function renderPage(client: QueryClient, initialEntries: string[] = ["/wishlist"]) {
  const container = document.createElement("div");
  document.body.appendChild(container);
  const root = createRoot(container);

  await act(async () => {
    root.render(
      <MemoryRouter initialEntries={initialEntries}>
        <QueryClientProvider client={client}>
          <WishlistPage />
        </QueryClientProvider>
      </MemoryRouter>
    );
  });

  await flush();

  return {
    container,
    root,
    cleanup: async () => {
      await act(async () => {
        root.unmount();
      });
      container.remove();
      client.clear();
    },
  };
}

describe("WishlistPage", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("includes the name query parameter when a search term is present", async () => {
    const getMock = vi.spyOn(http, "get").mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 50 },
    });

    const client = createClient();
    const { cleanup } = await renderPage(client, ["/wishlist?q=bolt&game=Magic"]);

    expect(getMock).toHaveBeenCalledWith(
      "user/42/wishlist",
      expect.objectContaining({
        params: expect.objectContaining({ name: "bolt", game: "Magic" }),
      })
    );

    await cleanup();
  });

  it("moves a single card and updates caches", async () => {
    const wishlistItem: WishlistItem = {
      cardPrintingId: 1001,
      quantityWanted: 2,
      cardId: 1,
      cardName: "Lightning Bolt",
      game: "Magic",
      set: "Alpha",
      number: "A1",
      rarity: "Common",
      style: "Standard",
      imageUrl: null,
    };
    const collectionItem: CollectionItem = {
      cardPrintingId: 1001,
      quantityOwned: 0,
      quantityWanted: 0,
      quantityProxyOwned: 0,
      availability: 0,
      availabilityWithProxies: 0,
      cardId: 1,
      cardName: "Lightning Bolt",
      game: "Magic",
      set: "Alpha",
      number: "A1",
      rarity: "Common",
      style: "Standard",
      imageUrl: null,
    };

    const getMock = vi.spyOn(http, "get").mockResolvedValue({
      data: ({ items: [wishlistItem], total: 1, page: 1, pageSize: 50 } satisfies WishlistPaged),
    });
    const postMock = vi.spyOn(http, "post").mockResolvedValue({
      data: {
        printingId: 1001,
        wantedAfter: 0,
        ownedAfter: 2,
        proxyAfter: 0,
        availability: 2,
        availabilityWithProxies: 2,
      },
    });
    const promptMock = vi.spyOn(window, "prompt").mockReturnValue("2");

    const client = createClient();
    const collectionKey = collectionKeys.list({
      userId: 42,
      page: 1,
      pageSize: 50,
      filters: { q: "", game: "", set: "", rarity: "" },
      includeProxies: false,
    });
    client.setQueryData(collectionKey, {
      items: [collectionItem],
      total: 1,
      page: 1,
      pageSize: 50,
    } as CollectionPaged<CollectionItem>);

    const { container, cleanup } = await renderPage(client);

    const button = Array.from(container.querySelectorAll("button")).find((b) =>
      b.textContent?.includes("Move to collection")
    );
    expect(button).not.toBeUndefined();

    await act(async () => {
      button?.click();
    });
    await flush();
    await flush();

    expect(postMock).toHaveBeenCalledWith("wishlist/move-to-collection", {
      cardPrintingId: 1001,
      quantity: 2,
      useProxy: false,
    });

    const toast = container.querySelector("div.mb-3");
    expect(toast?.textContent).toContain("Moved 2 cards to collection.");

    const wishlistData = client.getQueryData<WishlistPaged>(wishlistQueryKey);
    expect(wishlistData?.items).toHaveLength(0);

    const updatedCollection = client.getQueryData<CollectionPaged<CollectionItem>>(collectionKey);
    expect(updatedCollection?.items[0].quantityOwned).toBe(2);
    expect(updatedCollection?.items[0].availability).toBe(2);

    await cleanup();
    getMock.mockRestore();
    postMock.mockRestore();
    promptMock.mockRestore();
  });

  it("clamps quantities larger than wanted", async () => {
    const wishlistItem: WishlistItem = {
      cardPrintingId: 2001,
      quantityWanted: 3,
      cardId: 2,
      cardName: "Mickey",
      game: "Lorcana",
      set: "Spark",
      number: "S1",
      rarity: "Rare",
      style: "Standard",
      imageUrl: null,
    };

    vi.spyOn(http, "get").mockResolvedValue({
      data: ({ items: [wishlistItem], total: 1, page: 1, pageSize: 50 } satisfies WishlistPaged),
    });
    const postMock = vi.spyOn(http, "post").mockResolvedValue({
      data: {
        printingId: 2001,
        wantedAfter: 0,
        ownedAfter: 5,
        proxyAfter: 0,
        availability: 5,
        availabilityWithProxies: 5,
      },
    });
    const promptMock = vi.spyOn(window, "prompt").mockReturnValue("5");

    const client = createClient();
    const { container, cleanup } = await renderPage(client);

    const button = Array.from(container.querySelectorAll("button")).find((b) =>
      b.textContent?.includes("Move to collection")
    );

    await act(async () => {
      button?.click();
    });
    await flush();
    await flush();

    expect(postMock).toHaveBeenCalledWith("wishlist/move-to-collection", {
      cardPrintingId: 2001,
      quantity: 5,
      useProxy: false,
    });

    const toast = container.querySelector("div.mb-3");
    expect(toast?.textContent).toContain("Moved 3 cards to collection.");

    await cleanup();
    postMock.mockRestore();
    promptMock.mockRestore();
  });

  it("runs bulk moves and reports successes and failures", async () => {
    const items: WishlistItem[] = [
      {
        cardPrintingId: 3001,
        quantityWanted: 2,
        cardId: 3,
        cardName: "Alpha",
        game: "Magic",
        set: "Alpha",
        number: "A2",
        rarity: "Common",
        style: "Standard",
        imageUrl: null,
      },
      {
        cardPrintingId: 3002,
        quantityWanted: 1,
        cardId: 4,
        cardName: "Beta",
        game: "Magic",
        set: "Beta",
        number: "B2",
        rarity: "Uncommon",
        style: "Standard",
        imageUrl: null,
      },
    ];

    vi.spyOn(http, "get").mockResolvedValue({
      data: ({ items, total: 2, page: 1, pageSize: 50 } satisfies WishlistPaged),
    });

    const postMock = vi.spyOn(http, "post").mockImplementation((url, body: { cardPrintingId: number }) => {
      if (body.cardPrintingId === 3001) {
        return Promise.resolve({
          data: {
            printingId: 3001,
            wantedAfter: 0,
            ownedAfter: 2,
            proxyAfter: 0,
            availability: 2,
            availabilityWithProxies: 2,
          },
        });
      }
      return Promise.reject(new Error("boom"));
    });

    const promptMock = vi
      .spyOn(window, "prompt")
      .mockReturnValueOnce("1")
      .mockReturnValueOnce("1");

    const client = createClient();
    const { container, cleanup } = await renderPage(client);

    const checkboxes = Array.from(container.querySelectorAll<HTMLInputElement>("input[type=checkbox]"));
    checkboxes.forEach((box) => box.click());
    await flush();

    const bulkButton = Array.from(container.querySelectorAll("button")).find((b) =>
      b.textContent?.includes("Move selected")
    );
    expect(bulkButton).not.toBeUndefined();

    await act(async () => {
      bulkButton?.click();
    });
    await flush();
    await flush();

    expect(postMock).toHaveBeenCalledTimes(2);

    const toast = container.querySelector("div.mb-3");
    expect(toast?.textContent).toContain("Failed: Beta: boom");

    await cleanup();
    postMock.mockRestore();
    promptMock.mockRestore();
  });
});
