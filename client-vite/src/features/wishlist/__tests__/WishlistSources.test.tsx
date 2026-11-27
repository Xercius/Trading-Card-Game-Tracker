import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { MoveToCollectionDialog } from "../MoveToCollectionDialog";
import type { WishlistItemDto } from "../api";

describe("MoveToCollectionDialog with multiple wishlist sources", () => {
  const multiSourceItem: WishlistItemDto = {
    cardPrintingId: 101,
    quantityWanted: 3,
    cardId: 10,
    cardName: "Lightning Bolt",
    game: "Magic",
    set: "M10",
    number: "146",
    rarity: "Common",
    style: "Standard",
    imageUrl: null,
    sources: [
      { deckId: null, deckName: "General", quantity: 1 },
      { deckId: 5, deckName: "Burn Deck", quantity: 2 },
    ],
  };

  it("lets user select a specific source when multiple sources exist", async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(
      <MoveToCollectionDialog
        open={true}
        onOpenChange={vi.fn()}
        items={[multiSourceItem]}
        isPending={false}
        onConfirm={onConfirm}
      />
    );

    const select = screen.getByLabelText("Source for Lightning Bolt");
    fireEvent.change(select, { target: { value: "5" } });

    const submit = screen.getByRole("button", { name: "Move to collection" });
    fireEvent.click(submit);

    expect(onConfirm).toHaveBeenCalledTimes(1);
    const [, selectedSources] = onConfirm.mock.calls[0];
    expect(selectedSources.get(101)).toBe(5);
  });
});
