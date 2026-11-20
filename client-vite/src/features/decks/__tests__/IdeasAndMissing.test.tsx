import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import StickyDeckSidebar from "../StickyDeckSidebar";
import type { DeckCardWithAvailability } from "../api";

describe("StickyDeckSidebar Ideas & Missing Cards", () => {
  const rows: DeckCardWithAvailability[] = [
    {
      printingId: 1,
      cardName: "Sol Ring",
      imageUrl: null,
      quantityInDeck: 2,
      availability: 0,
      availabilityWithProxies: 0,
      missingQuantity: 2,
      quantityIdea: 0,
    },
    {
      printingId: 2,
      cardName: "Lightning Bolt",
      imageUrl: null,
      quantityInDeck: 0,
      availability: 1,
      availabilityWithProxies: 1,
      missingQuantity: 0,
      quantityIdea: 3,
    },
  ];

  it("shows missing badge, Add Missing button, and Ideas section", () => {
    const onAddMissingToWishlist = vi.fn();
    const onPromoteIdea = vi.fn();

    render(
      <StickyDeckSidebar
        deckName="Commander"
        rows={rows}
        includeProxies={false}
        onIncludeProxiesChange={vi.fn()}
        onAdjustQuantity={vi.fn()}
        onAddMissingToWishlist={onAddMissingToWishlist}
        onPromoteIdea={onPromoteIdea}
        valueHistory={{ points: [], isLoading: false, isError: false, latestValue: null }}
        formatCurrency={(v) => (v == null ? "—" : `$${v.toFixed(2)}`)}
      />
    );

    expect(screen.getByTestId("deck-row-1-missing")).toHaveTextContent("Missing 2");
    fireEvent.click(screen.getByTestId("add-all-missing-wishlist"));
    expect(onAddMissingToWishlist).toHaveBeenCalledTimes(1);

    const ideasSection = screen.getByTestId("deck-ideas-section");
    expect(ideasSection).toHaveTextContent("Ideas (3)");
    expect(ideasSection).toHaveTextContent("Lightning Bolt (3)");

    fireEvent.click(screen.getByRole("button", { name: "Move to Deck" }));
    expect(onPromoteIdea).toHaveBeenCalledWith(rows[1]);
  });
});
