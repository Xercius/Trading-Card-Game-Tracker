import { describe, it, expect, beforeAll, afterAll, beforeEach, afterEach, vi } from "vitest";
import { act } from "react-dom/test-utils";
import { createRoot, type Root } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import CardModal from "../CardModal";
import { api } from "@/lib/api";

const flushPromises = () => new Promise((resolve) => setTimeout(resolve, 0));

let originalIntersectionObserver: typeof window.IntersectionObserver | undefined;
let container: HTMLDivElement;
let root: Root;
let queryClient: QueryClient;
let getSpy: ReturnType<typeof vi.spyOn>;
let postSpy: ReturnType<typeof vi.spyOn>;

beforeAll(() => {
  Object.defineProperty(HTMLImageElement.prototype, "loading", {
    configurable: true,
    value: "lazy",
  });
  originalIntersectionObserver = globalThis.IntersectionObserver;
  class Observer implements IntersectionObserver {
    readonly root = null;
    readonly rootMargin = "";
    readonly thresholds: ReadonlyArray<number> = [];
    takeRecords(): IntersectionObserverEntry[] {
      return [];
    }
    observe(): void {}
    unobserve(): void {}
    disconnect(): void {}
  }
  globalThis.IntersectionObserver = Observer as unknown as typeof window.IntersectionObserver;
});

afterAll(() => {
  if (originalIntersectionObserver) {
    globalThis.IntersectionObserver = originalIntersectionObserver;
  } else {
    delete (globalThis as Record<string, unknown>).IntersectionObserver;
  }
});

beforeEach(() => {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  getSpy = vi.spyOn(api, "get");
  postSpy = vi.spyOn(api, "post");
});

afterEach(() => {
  root.unmount();
  queryClient.clear();
  container.remove();
  getSpy.mockRestore();
  postSpy.mockRestore();
});

function renderModal(props: { cardId: number; initialPrintingId?: number }) {
  return act(async () => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <CardModal
          open
          cardId={props.cardId}
          initialPrintingId={props.initialPrintingId}
          onOpenChange={() => {}}
        />
      </QueryClientProvider>
    );
    await flushPromises();
  });
}

describe("CardModal", () => {
  it("loads details, printings, and sparkline", async () => {
    getSpy.mockImplementation((url: string) => {
      if (url === "cards/100") {
        return Promise.resolve({
          data: {
            cardId: 100,
            name: "Test Card",
            game: "Magic",
            cardType: "Spell",
            description: "Deal 3 damage",
            printings: [],
          },
        });
      }
      if (url === "cards/100/printings") {
        return Promise.resolve({
          data: [
            {
              printingId: 1001,
              setName: "Alpha",
              setCode: null,
              number: "A1",
              rarity: "Common",
              imageUrl: "/alpha.png",
            },
            {
              printingId: 1002,
              setName: "Beta",
              setCode: null,
              number: "B2",
              rarity: "Uncommon",
              imageUrl: "/beta.png",
            },
          ],
        });
      }
      if (url === "cards/100/sparkline") {
        return Promise.resolve({
          data: [
            { d: "2024-01-01", v: 10 },
            { d: "2024-01-02", v: 12 },
          ],
        });
      }
      throw new Error(`Unexpected GET ${url}`);
    });

    await renderModal({ cardId: 100, initialPrintingId: 1001 });

    const label = container.querySelector('[data-testid="selected-printing-label"]');
    expect(label?.textContent).toContain("Alpha");

    const optionButtons = Array.from(container.querySelectorAll('button[role="option"]'));
    expect(optionButtons).toHaveLength(2);

    await act(async () => {
      optionButtons[1].click();
      await flushPromises();
    });

    expect(label?.textContent).toContain("Beta");
    const sparklineCalls = getSpy.mock.calls.filter((call) => call[0] === "cards/100/sparkline");
    expect(sparklineCalls.length).toBeGreaterThanOrEqual(1);
  });

  it("posts wishlist quick add with provided quantity", async () => {
    getSpy.mockImplementation((url: string) => {
      if (url === "cards/200") {
        return Promise.resolve({
          data: {
            cardId: 200,
            name: "Wishlist Card",
            game: "Magic",
            cardType: "Creature",
            description: "Test",
            printings: [],
          },
        });
      }
      if (url === "cards/200/printings") {
        return Promise.resolve({
          data: [
            {
              printingId: 2001,
              setName: "Gamma",
              setCode: null,
              number: "G1",
              rarity: "Rare",
              imageUrl: "/gamma.png",
            },
          ],
        });
      }
      if (url === "cards/200/sparkline") {
        return Promise.resolve({ data: [] });
      }
      throw new Error(`Unexpected GET ${url}`);
    });

    postSpy.mockImplementation((url: string, body: unknown) => {
      if (url === "wishlist/items") {
        return Promise.resolve({
          data: {
            printingId: (body as { printingId: number }).printingId,
            quantityWanted: (body as { quantity: number }).quantity,
          },
        });
      }
      throw new Error(`Unexpected POST ${url}`);
    });

    await renderModal({ cardId: 200, initialPrintingId: 2001 });

    const input = container.querySelector('input[type="number"]') as HTMLInputElement | null;
    expect(input).not.toBeNull();

    await act(async () => {
      if (!input) return;
      input.value = "2";
      input.dispatchEvent(new Event("input", { bubbles: true }));
      input.dispatchEvent(new Event("change", { bubbles: true }));
      await flushPromises();
    });

    const wishlistButton = Array.from(container.querySelectorAll("button")).find((btn) =>
      btn.textContent?.includes("Add to Wishlist")
    );
    expect(wishlistButton).not.toBeUndefined();

    await act(async () => {
      wishlistButton?.click();
      await flushPromises();
    });

    const call = postSpy.mock.calls.find((entry) => entry[0] === "wishlist/items");
    expect(call).toBeDefined();
    expect(call?.[1]).toEqual({ printingId: 2001, quantity: 2 });
  });

  it("shows empty state when no price data", async () => {
    getSpy.mockImplementation((url: string) => {
      if (url === "cards/300") {
        return Promise.resolve({
          data: {
            cardId: 300,
            name: "Price Card",
            game: "Magic",
            cardType: "Spell",
            description: "Price test",
            printings: [],
          },
        });
      }
      if (url === "cards/300/printings") {
        return Promise.resolve({
          data: [
            {
              printingId: 3001,
              setName: "Delta",
              setCode: null,
              number: "D1",
              rarity: "Mythic",
              imageUrl: "/delta.png",
            },
          ],
        });
      }
      if (url === "cards/300/sparkline") {
        return Promise.resolve({ data: [] });
      }
      throw new Error(`Unexpected GET ${url}`);
    });

    await renderModal({ cardId: 300, initialPrintingId: 3001 });

    const priceTab = Array.from(container.querySelectorAll("button")).find(
      (btn) => btn.textContent === "Price"
    );
    expect(priceTab).not.toBeUndefined();

    await act(async () => {
      priceTab?.click();
      await flushPromises();
    });

    expect(container.textContent).toContain("No value data.");
  });
});
