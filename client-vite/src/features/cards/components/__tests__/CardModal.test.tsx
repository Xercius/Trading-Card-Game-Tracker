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
import { act } from "react";
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
});

afterEach(() => {
  root.unmount();
  queryClient.clear();
  container.remove();
  getSpy.mockRestore();
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
  it("renders 3-column layout when card rulings exist", async () => {
    getSpy.mockImplementation((url: string) =>
      Promise.resolve({
        data: url.endsWith("/printings")
          ? [
              {
                printingId: 1001,
                setName: "Spark of Rebellion",
                setCode: "SOR",
                number: "001",
                rarity: "Rare",
                style: "Standard",
                releaseDate: "2025-10-03",
                imageUrl: "/front.png",
                backImageUrl: "/back.png",
              },
            ]
          : {
              id: 100,
              name: "Darth Vader \u2014 Dark Lord of the Sith",
              subtitle: "Dark Lord of the Sith",
              game: "Star Wars Unlimited",
              cardType: "Leader",
              cardType2: "Leader Unit",
              cardTypes: ["Leader", "Leader Unit"],
              arena: "Ground",
              aspects: [{ name: "Villainy", count: 1 }],
              traits: ["Force", "Imperial", "Sith"],
              cost: 7,
              power: 5,
              hp: 8,
              textStyled:
                '<p><strong>Action [</strong><img src="https://example.com/cost1.png" alt="1"><strong>]:</strong> Deal 1 damage.</p>',
              epicActionStyled: "<p><strong>Epic Action:</strong> Deploy this leader.</p>",
              deployBoxStyled: "<p><strong>When Deployed:</strong> Ready this unit.</p>",
              rulesStyled: "<p>If an ability refers to this leader...</p>",
              printings: [],
            },
      })
    );

    await renderModal({ cardId: 100, initialPrintingId: 1001 });

    // Center Column: Title without duplicate subtitle, Subtitle beneath
    expect(await screen.findByTestId("card-modal-card-name")).toHaveTextContent("Darth Vader");
    expect(screen.getByText("Dark Lord of the Sith")).toBeInTheDocument();

    // Aspects rendered as image to the right of card name
    const aspectContainer = screen.getByLabelText("Card aspects");
    expect(within(aspectContainer).getByAltText("Villainy")).toBeInTheDocument();

    // Stats row with SWU badge images and overlaid values
    const statsRow = screen.getByLabelText("Card stats");
    expect(within(statsRow).getByAltText("Cost badge")).toBeInTheDocument();
    const costValue = within(statsRow).getByText("7");
    expect(costValue).toBeInTheDocument();
    expect(costValue.className).toContain("text-white");

    expect(within(statsRow).getByAltText("Power badge")).toBeInTheDocument();
    const powerValue = within(statsRow).getByText("5");
    expect(powerValue).toBeInTheDocument();
    expect(powerValue.className).toContain("text-white");

    expect(within(statsRow).getByAltText("HP badge")).toBeInTheDocument();
    const hpValue = within(statsRow).getByText("8");
    expect(hpValue).toBeInTheDocument();
    expect(hpValue.className).toContain("text-white");

    // Traits placed directly beneath stats without subtitle
    expect(screen.getByText("Force, Imperial, Sith")).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Traits" })).toBeNull();

    // Front text headed by card type + arena (replaces "Card Text" subtitle)
    expect(screen.getByRole("heading", { name: "Leader • Ground" })).toBeInTheDocument();
    expect(screen.getByText("Action [").tagName.toLowerCase()).toBe("strong");
    expect(screen.getByAltText("1")).toBeInTheDocument();

    // Epic Action rendered
    expect(screen.getByText("Deploy this leader.", { exact: false })).toBeInTheDocument();

    // Back text headed by back card type (replaces "Back Text" subtitle)
    expect(screen.getByRole("heading", { name: "Leader Unit" })).toBeInTheDocument();
    expect(screen.getByText("Ready this unit.", { exact: false })).toBeInTheDocument();

    // Left Column: Front on top, Back on bottom
    const imageGroup = screen.getByRole("group", { name: "Card images" });
    const images = within(imageGroup).getAllByRole("img");
    expect(images).toHaveLength(2);
    expect(images[0].getAttribute("alt")).toBe("Darth Vader — Dark Lord of the Sith (front)");
    expect(images[1].getAttribute("alt")).toBe("Darth Vader — Dark Lord of the Sith (back)");
    expect(imageGroup.classList.contains("grid-rows-2")).toBe(true);

    // Left Column: Printing metadata below card image (Row 1: Rarity & Treatment; Row 2: Set Logo & Number)
    const metadata = screen.getByTestId("selected-printing-metadata");
    expect(within(metadata).getByText("Rare")).toBeInTheDocument();
    expect(within(metadata).getByText("Standard")).toBeInTheDocument();
    expect(within(metadata).getByAltText("Spark of Rebellion")).toBeInTheDocument();
    expect(within(metadata).getByText("#001")).toBeInTheDocument();
    // Subtitles omitted per spec
    expect(within(metadata).queryByText("Rarity")).toBeNull();
    expect(within(metadata).queryByText("Set Name")).toBeNull();
    expect(within(metadata).queryByText("Set Number")).toBeNull();
    expect(within(metadata).queryByText("Card Treatment")).toBeNull();

    // Right Column: Rulings section present
    const rulingsSection = screen.getByRole("region", { name: "Card rulings" });
    expect(rulingsSection).toBeInTheDocument();
    expect(
      within(rulingsSection).getByText("If an ability refers to this leader...")
    ).toBeInTheDocument();

    // Bottom section: Printings list with set code and set number
    const printingsSection = screen.getByRole("region", { name: "Card printings" });
    expect(within(printingsSection).getByText("SOR #001")).toBeInTheDocument();

    // Close button present and accessible
    expect(screen.getByLabelText("Close card dialog")).toBeInTheDocument();

    // Verify Collection & Wishlist quick-add buttons and quantity inputs are removed
    expect(screen.queryByLabelText("Add to collection")).toBeNull();
    expect(screen.queryByLabelText("Add to wishlist")).toBeNull();
    expect(screen.queryByRole("spinbutton")).toBeNull();
  });

  it("omits Epic Action section when card has empty epicActionStyled or empty string (e.g. 4-LOM)", async () => {
    getSpy.mockImplementation((url: string) =>
      Promise.resolve({
        data: url.endsWith("/printings")
          ? []
          : {
              id: 105,
              name: "4-LOM \u2014 Devious",
              subtitle: "Devious",
              game: "Star Wars Unlimited",
              cardType: "Unit",
              arena: "Ground",
              traits: ["Bounty Hunter", "Droid", "Underworld"],
              cost: 5,
              power: 4,
              hp: 5,
              textStyled:
                "<p><strong>When Played:</strong> You may attack with a friendly <i>Bounty Hunter</i> unit.</p>",
              epicAction: "",
              epicActionStyled: "<p></p>",
              rulesStyled: null,
              printings: [],
            },
      })
    );

    await renderModal({ cardId: 105 });

    expect(await screen.findByTestId("card-modal-card-name")).toHaveTextContent("4-LOM");
    // Ensure Epic Action is completely omitted when empty
    expect(screen.queryByRole("heading", { name: "Epic Action" })).toBeNull();
    expect(screen.queryByText("Epic Action")).toBeNull();
  });

  it("renders table-based rulings from RulesStyled with date and description columns", async () => {
    getSpy.mockImplementation((url: string) =>
      Promise.resolve({
        data: url.endsWith("/printings")
          ? []
          : {
              id: 102,
              name: "A Fine Addition",
              game: "Star Wars Unlimited",
              cardType: "Event",
              cardTypes: ["Event"],
              text: "Play a unit or upgrade...",
              rulesStyled:
                '<figure class="table"><table><tbody><tr><td>10/31/2024</td><td>Abilities that let you play a card require you to pay that card\'s cost unless specified otherwise.</td></tr><tr><td>07/20/2026</td><td>(ERRATA) Templating update: "Aspect penalty" becomes "aspect penalties" when referring to multiple.</td></tr></tbody></table></figure>',
              printings: [],
            },
      })
    );

    await renderModal({ cardId: 102 });

    const rulingsSection = await screen.findByRole("region", { name: "Card rulings" });
    expect(rulingsSection).toBeInTheDocument();

    const table = within(rulingsSection).getByRole("table");
    expect(table).toBeInTheDocument();

    const rows = within(table).getAllByRole("row");
    expect(rows).toHaveLength(2);

    const firstRowCells = within(rows[0]).getAllByRole("cell");
    expect(firstRowCells[0]).toHaveTextContent("10/31/2024");
    expect(firstRowCells[1]).toHaveTextContent(
      "Abilities that let you play a card require you to pay that card's cost unless specified otherwise."
    );

    const secondRowCells = within(rows[1]).getAllByRole("cell");
    expect(secondRowCells[0]).toHaveTextContent("07/20/2026");
    expect(secondRowCells[1]).toHaveTextContent(
      '(ERRATA) Templating update: "Aspect penalty" becomes "aspect penalties" when referring to multiple.'
    );
  });

  it("omits 3rd rulings column when rulesStyled is absent or whitespace", async () => {
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
              text: "Defeat a unit with 5 or less remaining HP.",
              rulesStyled: null,
              rules: null,
              printings: [],
            },
      })
    );

    await renderModal({ cardId: 101 });

    expect(await screen.findByTestId("card-modal-card-name")).toHaveTextContent("Takedown");
    expect(screen.queryByRole("region", { name: "Card rulings" })).toBeNull();
    expect(screen.queryByRole("heading", { name: "Rulings" })).toBeNull();
  });

  it("stacks front and back images with front on top and back on bottom", async () => {
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
              text: null,
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
    expect(group.classList.contains("grid-rows-2")).toBe(true);

    // Switching to single-sided printing
    await act(async () => {
      screen.getByRole("option", { name: "Beta #2" }).click();
    });

    const singleImage = within(group).getAllByRole("img");
    expect(singleImage).toHaveLength(1);
    expect(singleImage[0].getAttribute("src")).toMatch(/\/single.png$/);
    expect(group.classList.contains("grid-cols-1")).toBe(true);
    expect(group.classList.contains("grid-rows-1")).toBe(true);
  });

  it("updates selected printing metadata and images when a printing is selected from the bottom carousel", async () => {
    getSpy.mockImplementation((url: string) => {
      if (url === "cards/100") {
        return Promise.resolve({
          data: {
            cardId: 100,
            name: "Test Card",
            game: "Star Wars Unlimited",
            cardType: "Spell",
            text: "Deal 3 damage.",
            printings: [],
          },
        });
      }
      if (url === "cards/100/printings") {
        return Promise.resolve({
          data: [
            {
              printingId: 1001,
              setName: "Alpha Set",
              setCode: "ALP",
              number: "001",
              rarity: "Common",
              style: "Standard",
              imageUrl: "/alpha.png",
            },
            {
              printingId: 1002,
              setName: "Beta Set",
              setCode: "BET",
              number: "002",
              rarity: "Legendary",
              style: "Hyperspace Foil",
              imageUrl: "/beta.png",
            },
          ],
        });
      }
      throw new Error(`Unexpected GET ${url}`);
    });

    await renderModal({ cardId: 100, initialPrintingId: 1001 });

    const metadata = await screen.findByTestId("selected-printing-metadata");
    await waitFor(() => expect(within(metadata).getByAltText("Alpha Set")).toBeInTheDocument());
    expect(within(metadata).getByText("Common")).toBeInTheDocument();
    expect(within(metadata).getByText("#001")).toBeInTheDocument();
    expect(within(metadata).getByText("Standard")).toBeInTheDocument();
    fireEvent.error(within(metadata).getByAltText("Alpha Set"));

    const options = screen.getAllByRole("option");
    expect(options).toHaveLength(2);
    expect(within(options[0]).getByText("ALP #001")).toBeInTheDocument();
    expect(within(options[1]).getByText("BET #002")).toBeInTheDocument();

    // Select second printing
    await act(async () => {
      options[1].click();
    });

    await waitFor(() => expect(within(metadata).getByAltText("Beta Set")).toBeInTheDocument());
    expect(within(metadata).getByText("Legendary")).toBeInTheDocument();
    expect(within(metadata).getByText("#002")).toBeInTheDocument();
    expect(within(metadata).getByText("Hyperspace Foil")).toBeInTheDocument();
  });

  it("keeps the dialog accessibly named when card details fail to load", async () => {
    getSpy.mockImplementation((url: string) =>
      url.endsWith("/printings")
        ? Promise.resolve({ data: [] })
        : Promise.reject(new Error("Request failed"))
    );

    await renderModal({ cardId: 100 });

    await screen.findByText("Failed to load card details.");
    expect(screen.getByRole("dialog", { name: "Card details" })).toBeInTheDocument();
  });
});
