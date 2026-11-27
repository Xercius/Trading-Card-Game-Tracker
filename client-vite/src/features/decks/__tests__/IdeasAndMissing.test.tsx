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

  it("shows only game-appropriate zone and role controls", () => {
    const onToggleCommander = vi.fn();
    const onToggleStarting = vi.fn();
    const onAdjustSideboard = vi.fn();

    const { rerender } = render(
      <StickyDeckSidebar
        deckName="Commander Deck"
        game="Magic The Gathering"
        format="Commander"
        rows={[
          {
            printingId: 10,
            cardName: "Atraxa",
            imageUrl: null,
            quantityInDeck: 1,
            availability: 1,
            availabilityWithProxies: 1,
            canBeCommander: true,
            isCommander: true,
            cardType: "Creature",
          },
        ]}
        includeProxies={false}
        onIncludeProxiesChange={vi.fn()}
        onAdjustQuantity={vi.fn()}
        onToggleCommander={onToggleCommander}
        onToggleStarting={onToggleStarting}
        onAdjustSideboard={onAdjustSideboard}
        valueHistory={{ points: [], isLoading: false, isError: false, latestValue: null }}
        formatCurrency={(v) => (v == null ? "—" : `$${v.toFixed(2)}`)}
      />
    );

    expect(screen.getByTestId("deck-zone-commander")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "★ Cmdr" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Start/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "+SB" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "★ Cmdr" }));
    expect(onToggleCommander).toHaveBeenCalledTimes(1);

    rerender(
      <StickyDeckSidebar
        deckName="SWCCG Deck"
        game="Star Wars CCG"
        format="Standard"
        side="Light"
        rows={[
          {
            printingId: 20,
            cardName: "Luke Skywalker",
            imageUrl: null,
            quantityInDeck: 1,
            availability: 1,
            availabilityWithProxies: 1,
            isStarting: false,
            cardType: "Character",
          },
        ]}
        includeProxies={false}
        onIncludeProxiesChange={vi.fn()}
        onAdjustQuantity={vi.fn()}
        onToggleCommander={onToggleCommander}
        onToggleStarting={onToggleStarting}
        onAdjustSideboard={onAdjustSideboard}
        valueHistory={{ points: [], isLoading: false, isError: false, latestValue: null }}
        formatCurrency={(v) => (v == null ? "—" : `$${v.toFixed(2)}`)}
      />
    );

    expect(screen.queryByRole("button", { name: /Cmdr/ })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "☆ Start" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "☆ Start" }));
    expect(onToggleStarting).toHaveBeenCalledTimes(1);
  });
});
