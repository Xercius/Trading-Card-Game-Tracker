import {
  describe,
  it,
  expect,
  beforeAll,
  afterAll,
  beforeEach,
  afterEach,
  vi,
  type MockInstance,
} from "vitest";
import { act } from "react-dom/test-utils";
import { createRoot, type Root } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import CardModal from "../CardModal";
import { api } from "@/lib/api";

let originalIntersectionObserver: typeof window.IntersectionObserver | undefined;
let container: HTMLDivElement;
let root: Root;
let queryClient: QueryClient;
let getSpy: MockInstance<(url: string) => Promise<unknown>>;
let postSpy: MockInstance<(url: string, body: unknown) => Promise<unknown>>;

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
  it("renders normalized types, aspect multiplicity, set, rarity, and release date", async () => {
    getSpy.mockImplementation((url: string) =>
      Promise.resolve({
        data: url.endsWith("/printings")
          ? [
              {
                printingId: 1001,
                setName: "Spark of Rebellion",
                setCode: "SOR",
                number: "SOR-001",
                rarity: "Rare",
                style: "Standard",
                releaseDate: "2025-10-03",
                imageUrl: "/front.png",
              },
            ]
          : {
              id: 100,
              name: "Luke Skywalker",
              game: "Star Wars Unlimited",
              cardType: "Leader",
              cardType2: "Leader Unit",
              cardTypes: ["Leader", "Leader Unit"],
              arena: "Ground",
              aspects: [{ name: "Heroism", count: 2 }],
              traits: ["Force", "Rebel"],
              cost: 6,
              power: 4,
              hp: 7,
              textStyled:
                "<p><strong>On Attack:</strong> Give an Experience token to a friendly <strong><i>Vehicle</i></strong> or <strong><i>Droid</i></strong> unit.</p>",
              epicActionStyled: "<p><strong>Epic Action:</strong> Deploy this leader.</p>",
              deployBoxStyled: "<p><strong>When Deployed:</strong> Give a Shield token.</p>",
              description: null,
              printings: [],
            },
      })
    );

    await renderModal({ cardId: 100, initialPrintingId: 1001 });

    expect(await screen.findByText("Leader")).toBeInTheDocument();
    expect(screen.getByText("Type 2:")).toBeInTheDocument();
    expect(screen.getByText("Leader Unit")).toBeInTheDocument();
    expect(screen.getByText("Arena:")).toBeInTheDocument();
    expect(screen.getByText("Ground")).toBeInTheDocument();
    expect(screen.getByText("Heroism ×2")).toBeInTheDocument();
    expect(screen.getByText("Force, Rebel")).toBeInTheDocument();
    expect(screen.getByText("Cost:")).toBeInTheDocument();
    expect(screen.getByText("6")).toBeInTheDocument();
    expect(screen.getByText("Power:")).toBeInTheDocument();
    expect(screen.getByText("4")).toBeInTheDocument();
    expect(screen.getByText("HP:")).toBeInTheDocument();
    expect(screen.getByText("7")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Rules Text" })).toBeInTheDocument();
    expect(screen.getByText("On Attack:").tagName.toLowerCase()).toBe("strong");
    expect(screen.getByText("Vehicle").tagName.toLowerCase()).toBe("i");
    expect(screen.getByRole("heading", { name: "Epic Action" })).toBeInTheDocument();
    expect(screen.getByText("Deploy this leader.", { exact: false })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Deploy Box" })).toBeInTheDocument();
    expect(screen.getByText("Give a Shield token.", { exact: false })).toBeInTheDocument();
    expect(screen.getByTestId("selected-printing-label").textContent).toContain(
      "Spark of Rebellion (SOR) • #SOR-001 • Rare • 2025-10-03 • Standard"
    );
  });

  it("hides Type 2 when absent and hides Arena when card type does not contain Unit", async () => {
    getSpy.mockImplementation((url: string) =>
      Promise.resolve({
        data: url.endsWith("/printings")
          ? []
          : {
              id: 101,
              name: "Takedown",
              game: "Star Wars Unlimited",
              cardType: "Event",
              cardTypes: ["Event"],
              arena: "Ground",
              description: "Defeat a unit.",
              printings: [],
            },
      })
    );

    await renderModal({ cardId: 101 });

    expect(await screen.findByText("Event")).toBeInTheDocument();
    expect(screen.queryByText("Type 2:")).toBeNull();
    expect(screen.queryByText("Arena:")).toBeNull();
    expect(screen.queryByRole("heading", { name: "Epic Action" })).toBeNull();
    expect(screen.queryByRole("heading", { name: "Deploy Box" })).toBeNull();
  });

  it.each([
    ["vertical", "vertical", false],
    ["horizontal", "horizontal", true],
    ["vertical", "horizontal", false],
    ["horizontal", "vertical", false],
    ["horizontal", null, false],
    [null, "horizontal", false],
    [null, null, false],
    ["unknown", "horizontal", false],
  ])(
    "orders front/back images with orientations %s/%s (stacked: %s)",
    async (imageOrientation, backImageOrientation, stacked) => {
      getSpy.mockImplementation((url: string) =>
        Promise.resolve({
          data: url.endsWith("/printings")
            ? [
                {
                  printingId: 1001,
                  setName: "Alpha",
                  number: "1",
                  rarity: "Rare",
                  imageUrl: "/front.png",
                  backImageUrl: "/back.png",
                  imageOrientation,
                  backImageOrientation,
                },
                {
                  printingId: 1002,
                  setName: "Beta",
                  number: "2",
                  rarity: "Common",
                  imageUrl: "/single.png",
                },
              ]
            : {
                cardId: 100,
                name: "Two-sided Card",
                game: "Magic",
                cardType: "Creature",
                description: null,
                printings: [],
              },
        })
      );

      await renderModal({ cardId: 100, initialPrintingId: 1001 });

      const group = await screen.findByRole("group", { name: "Card images" });
      await waitFor(() => expect(within(group).getAllByRole("img")).toHaveLength(2));
      const images = within(group).getAllByRole("img");
      expect(images[0].getAttribute("alt")).toBe("Two-sided Card (front)");
      expect(images[0].getAttribute("src")).toMatch(/\/front.png$/);
      expect(images[1].getAttribute("alt")).toBe("Two-sided Card (back)");
      expect(images[1].getAttribute("src")).toMatch(/\/back.png$/);
      expect(group.classList.contains(stacked ? "grid-rows-2" : "grid-cols-2")).toBe(true);

      await act(async () => {
        screen.getByRole("option", { name: "Beta #2" }).click();
      });

      const singleImage = within(group).getAllByRole("img");
      expect(singleImage).toHaveLength(1);
      expect(singleImage[0].getAttribute("src")).toMatch(/\/single.png$/);
      expect(group.classList.contains("grid-cols-1")).toBe(true);
      expect(group.classList.contains("grid-rows-1")).toBe(true);
    }
  );

  it("uses image metadata from card details when printing summaries omit it", async () => {
    getSpy.mockImplementation((url: string) =>
      Promise.resolve({
        data: url.endsWith("/printings")
          ? [{ printingId: 1001, setName: "Alpha", number: "1", rarity: "Rare" }]
          : {
              cardId: 100,
              name: "Two-sided Card",
              game: "Magic",
              cardType: "Creature",
              description: null,
              printings: [
                {
                  id: 1001,
                  imageUrl: "/front.png",
                  backImageUrl: "/back.png",
                  imageOrientation: "horizontal",
                  backImageOrientation: "horizontal",
                },
              ],
            },
      })
    );

    await renderModal({ cardId: 100, initialPrintingId: 1001 });

    const group = await screen.findByRole("group", { name: "Card images" });
    await waitFor(() => expect(within(group).getAllByRole("img")).toHaveLength(2));
    expect(group.classList.contains("grid-rows-2")).toBe(true);
  });

  it("keeps the placeholder when neither image is available", async () => {
    getSpy.mockImplementation((url: string) =>
      Promise.resolve({
        data: url.endsWith("/printings")
          ? []
          : {
              cardId: 100,
              name: "No Image Card",
              game: "Magic",
              cardType: "Creature",
              description: null,
              printings: [],
            },
      })
    );

    await renderModal({ cardId: 100 });

    const group = screen.getByRole("group", { name: "Card images" });
    expect(within(group).queryByRole("img")).toBeNull();
    expect(within(group).getByText("No image available")).toBeInTheDocument();
  });

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
