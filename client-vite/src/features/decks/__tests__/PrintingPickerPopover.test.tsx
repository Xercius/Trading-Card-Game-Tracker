import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { PrintingPickerPopover } from "../components/PrintingPickerPopover";

vi.mock("@/features/cards/api", () => ({
  useCardPrintings: (cardId: number) =>
    cardId > 0
      ? {
          isLoading: false,
          data: [
            {
              printingId: 1,
              cardId: 10,
              setName: "Alpha",
              setCode: "LEA",
              number: "1",
              style: "Standard",
              imageUrl: null,
            },
            {
              printingId: 2,
              cardId: 10,
              setName: "Beta",
              setCode: "LEB",
              number: "2",
              style: "Foil",
              imageUrl: null,
            },
          ],
        }
      : { isLoading: false, data: [] },
}));

describe("PrintingPickerPopover", () => {
  it("opens printings list and invokes onSelectPrinting when a printing is clicked", () => {
    const onSelectPrinting = vi.fn();
    render(
      <PrintingPickerPopover
        cardId={10}
        currentPrintingId={1}
        currentLabel="Alpha"
        ariaLabel="Switch printing for Lightning Bolt"
        onSelectPrinting={onSelectPrinting}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Switch printing for Lightning Bolt" }));

    const betaOption = screen.getByRole("option", { name: /Beta #2/i });
    fireEvent.click(betaOption);

    expect(onSelectPrinting).toHaveBeenCalledTimes(1);
    expect(onSelectPrinting).toHaveBeenCalledWith(2, "Beta #2 · Foil");
  });
});
