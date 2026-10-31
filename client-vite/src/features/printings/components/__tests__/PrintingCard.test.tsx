import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { describe, expect, it, afterEach } from "vitest";
import { PrintingCard } from "../PrintingCard";
import { isHorizontalPrinting } from "../../utils/cardOrientation";
import type { PrintingListItem } from "../../api/printings";

describe("PrintingCard", () => {
  const mockPrinting: PrintingListItem = {
    printingId: "p1",
    cardId: "c1",
    cardName: "Sample Card",
    game: "Test Game",
    setName: "Test Set",
    setCode: "TST",
    number: "001",
    rarity: "Rare",
    imageUrl: "https://example.com/card.jpg",
    cardTypes: ["Leader", "Leader Unit"],
    aspects: [{ name: "Heroism", count: 2 }],
    releaseDate: "2025-10-03",
  };

  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("renders with accessible name including card name, set, and number", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={mockPrinting} />);
    });

    const button = container.querySelector<HTMLButtonElement>("button");
    expect(button).not.toBeNull();
    expect(button?.getAttribute("aria-label")).toBe("Sample Card — Test Set #001");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("renders accessible name without number when not provided", async () => {
    const printingWithoutNumber = { ...mockPrinting, number: null };
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={printingWithoutNumber} />);
    });

    const button = container.querySelector<HTMLButtonElement>("button");
    expect(button).not.toBeNull();
    expect(button?.getAttribute("aria-label")).toBe("Sample Card — Test Set");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("does not display visible text for cards with artwork", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={mockPrinting} />);
    });

    // Image should have empty alt since button has aria-label
    const img = container.querySelector("img");
    expect(img).not.toBeNull();
    expect(img?.getAttribute("alt")).toBe("");

    const textContent = container.textContent || "";
    expect(textContent).not.toContain("Sample Card");
    expect(textContent).not.toContain("Test Set");
    expect(textContent).not.toContain("001");
    expect(textContent).not.toContain("Rare");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("lets landscape images fill the card width without a fixed height", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={mockPrinting} />);
    });

    const img = container.querySelector("img");
    expect(img?.classList.contains("w-full")).toBe(true);
    expect(img?.classList.contains("h-auto")).toBe(true);
    expect(img?.parentElement?.classList.contains("aspect-[63/88]")).toBe(false);

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("identifies cards with missing artwork", async () => {
    const printingWithoutImage = { ...mockPrinting, imageUrl: null };
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={printingWithoutImage} />);
    });

    expect(container.textContent).toContain("Sample Card");
    expect(container.textContent).toContain("Test Set");
    expect(container.textContent).toContain("No image");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("identifies cards that use placeholder artwork", async () => {
    const printingWithPlaceholder = {
      ...mockPrinting,
      imageUrl: "/images/placeholders/card-3x4.png",
    };
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={printingWithPlaceholder} />);
    });

    expect(container.querySelector("img")).toBeNull();
    expect(container.textContent).toContain("Sample Card");
    expect(container.textContent).toContain("Test Set");

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("applies horizontal aspect ratio for placeholder when card is horizontal", async () => {
    const horizontalPlaceholder = {
      ...mockPrinting,
      imageUrl: null,
      imageOrientation: "horizontal" as const,
    };
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={horizontalPlaceholder} />);
    });

    const placeholder = container.querySelector(".aspect-\\[88\\/63\\]");
    expect(placeholder).not.toBeNull();

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("applies vertical aspect ratio for placeholder when card is vertical", async () => {
    const verticalPlaceholder = {
      ...mockPrinting,
      imageUrl: null,
      cardTypes: ["Unit"],
      imageOrientation: "vertical" as const,
    };
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PrintingCard p={verticalPlaceholder} />);
    });

    const placeholder = container.querySelector(".aspect-\\[63\\/88\\]");
    expect(placeholder).not.toBeNull();

    await act(async () => {
      root.unmount();
    });
    container.remove();
  });

  it("isHorizontalPrinting correctly identifies horizontal orientations and types", () => {
    expect(isHorizontalPrinting({ ...mockPrinting, imageOrientation: "horizontal" })).toBe(true);
    expect(isHorizontalPrinting({ ...mockPrinting, imageOrientation: "vertical" })).toBe(false);
    expect(
      isHorizontalPrinting({ ...mockPrinting, imageOrientation: null, cardTypes: ["Leader"] })
    ).toBe(true);
    expect(
      isHorizontalPrinting({ ...mockPrinting, imageOrientation: null, cardTypes: ["Base"] })
    ).toBe(true);
    expect(
      isHorizontalPrinting({ ...mockPrinting, imageOrientation: null, cardTypes: ["Unit"] })
    ).toBe(false);
  });
});
