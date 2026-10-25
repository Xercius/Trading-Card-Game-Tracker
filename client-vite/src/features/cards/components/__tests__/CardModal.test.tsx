import { describe, it, expect, beforeAll, afterAll, beforeEach, afterEach, vi } from "vitest";
import { act } from "react-dom/test-utils";
import { createRoot, type Root } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import CardModal from "../CardModal";
import { api } from "@/lib/api";

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
  });
}

describe("CardModal", () => {
  it("loads details, printings, and shows them inline", async () => {
    getSpy.mockImplementation((url: string) => {
      if (url === "cards/100") {
        return Promise.resolve({
          data: {
            cardId: 100,
            name: "Test Card",
            game: "Magic",
            cardType: "Spell",
            description:
              "Deal 3 damage.\nSecond line.\nThird line.\nFourth line.\nFifth line.\nSixth line.\nSeventh line remains readable.",
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
      throw new Error(`Unexpected GET ${url}`);
    });

    await renderModal({ cardId: 100, initialPrintingId: 1001 });

    const label = await screen.findByTestId("selected-printing-label");
    await waitFor(() => expect(label.textContent).toContain("Alpha"));

    const dialog = screen.getByRole("dialog");
    const detailsSection = dialog?.querySelector('[aria-label="Card details and actions"]');
    const imageSection = dialog?.querySelector('[aria-label="Selected card image"]');
    const printingsSection = dialog?.querySelector('[aria-label="Card printings"]');
    const rulesText = detailsSection?.querySelector("p.whitespace-pre-line");
    expect(dialog?.classList.contains("bg-[#242424]")).toBe(true);
    expect(rulesText?.textContent).toContain("Seventh line remains readable.");
    expect(rulesText?.classList.contains("line-clamp-6")).toBe(false);
    expect(rulesText?.closest(".overflow-y-auto")).not.toBeNull();
    expect(detailsSection?.querySelector('input[type="number"]')).not.toBeNull();
    expect(detailsSection?.querySelector('[aria-label="Add to collection"]')).not.toBeNull();
    expect(detailsSection?.querySelector('[aria-label="Add to wishlist"]')).not.toBeNull();
    expect(detailsSection?.nextElementSibling).toBe(imageSection);
    expect(detailsSection?.contains(printingsSection)).toBe(true);
    expect(printingsSection?.previousElementSibling?.textContent).toContain(
      "Seventh line remains readable."
    );

    const optionButtons = screen.getAllByRole("option");
    expect(optionButtons).toHaveLength(2);

    await act(async () => {
      optionButtons[1].click();
    });

    await waitFor(() => expect(label.textContent).toContain("Beta"));
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

    const input = await screen.findByRole("spinbutton");

    fireEvent.change(input, { target: { value: "2" } });

    const wishlistButton = screen.getByRole("button", { name: "Add to wishlist" });

    await act(async () => {
      wishlistButton.click();
    });

    await waitFor(() => {
      const call = postSpy.mock.calls.find((entry) => entry[0] === "wishlist/items");
      expect(call?.[1]).toEqual({ printingId: 2001, quantity: 2 });
    });
  });
});
