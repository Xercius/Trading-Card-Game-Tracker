import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { CardFiltersDrawer } from "../CardFiltersDrawer";
import type { CardFilters } from "@/features/cards/filters/useCardFilters";
import type { PrintingFacets } from "../../api/usePrintingFacets";

const defaultFilters: CardFilters = {
  q: "",
  games: [],
  sets: [],
  rarities: [],
  types: [],
  arenas: [],
  aspects: [],
  aspectMethod: "or",
  traits: [],
  traitMethod: "or",
  keywords: [],
  keywordMethod: "or",
  page: 1,
  pageSize: 60,
};

const defaultFacets: PrintingFacets = {
  games: ["Star Wars Unlimited"],
  sets: ["Spark of Rebellion"],
  rarities: ["Common", "Rare"],
  types: ["Leader", "Unit"],
  arenas: ["Ground", "Space"],
  aspects: ["Aggression", "Command"],
  traits: ["Rebel", "Imperial"],
  keywords: ["Sentinel", "Grit"],
};

describe("CardFiltersDrawer", () => {
  afterEach(() => {
    cleanup();
  });

  it("does not render when open is false", () => {
    render(
      <CardFiltersDrawer
        open={false}
        onOpenChange={vi.fn()}
        filters={defaultFilters}
        setFilters={vi.fn()}
        availableFacets={defaultFacets}
        isFacetsLoading={false}
        isFacetsError={false}
        onRetryFacets={vi.fn()}
        onClearFilters={vi.fn()}
        hasActiveFilters={false}
      />
    );

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("renders all filter options and footer buttons when open is true", () => {
    render(
      <CardFiltersDrawer
        open={true}
        onOpenChange={vi.fn()}
        filters={defaultFilters}
        setFilters={vi.fn()}
        availableFacets={defaultFacets}
        isFacetsLoading={false}
        isFacetsError={false}
        onRetryFacets={vi.fn()}
        onClearFilters={vi.fn()}
        hasActiveFilters={false}
      />
    );

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Filters" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Game" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Aspects" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Type" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Set" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Rarity" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Arena" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Traits" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Keywords" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Clear Filters" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Close" })).toBeInTheDocument();
  });

  it("calls onOpenChange(false) when clicking the close button in header", () => {
    const handleOpenChange = vi.fn();
    render(
      <CardFiltersDrawer
        open={true}
        onOpenChange={handleOpenChange}
        filters={defaultFilters}
        setFilters={vi.fn()}
        availableFacets={defaultFacets}
        isFacetsLoading={false}
        isFacetsError={false}
        onRetryFacets={vi.fn()}
        onClearFilters={vi.fn()}
        hasActiveFilters={false}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Close filters" }));
    expect(handleOpenChange).toHaveBeenCalledWith(false);
  });

  it("calls onOpenChange(false) when clicking the close button in footer", () => {
    const handleOpenChange = vi.fn();
    render(
      <CardFiltersDrawer
        open={true}
        onOpenChange={handleOpenChange}
        filters={defaultFilters}
        setFilters={vi.fn()}
        availableFacets={defaultFacets}
        isFacetsLoading={false}
        isFacetsError={false}
        onRetryFacets={vi.fn()}
        onClearFilters={vi.fn()}
        hasActiveFilters={false}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Close" }));
    expect(handleOpenChange).toHaveBeenCalledWith(false);
  });

  it("calls onOpenChange(false) when pressing the Escape key", () => {
    const handleOpenChange = vi.fn();
    render(
      <CardFiltersDrawer
        open={true}
        onOpenChange={handleOpenChange}
        filters={defaultFilters}
        setFilters={vi.fn()}
        availableFacets={defaultFacets}
        isFacetsLoading={false}
        isFacetsError={false}
        onRetryFacets={vi.fn()}
        onClearFilters={vi.fn()}
        hasActiveFilters={false}
      />
    );

    fireEvent.keyDown(document, { key: "Escape" });
    expect(handleOpenChange).toHaveBeenCalledWith(false);
  });

  it("moves focus into the drawer, contains Tab, and restores focus on close", async () => {
    const handleOpenChange = vi.fn();
    const renderDrawer = (open: boolean) => (
      <>
        <button type="button">Open filters</button>
        <CardFiltersDrawer
          open={open}
          onOpenChange={handleOpenChange}
          filters={defaultFilters}
          setFilters={vi.fn()}
          availableFacets={defaultFacets}
          isFacetsLoading={false}
          isFacetsError={false}
          onRetryFacets={vi.fn()}
          onClearFilters={vi.fn()}
          hasActiveFilters={false}
        />
      </>
    );
    const { rerender } = render(renderDrawer(false));
    const opener = screen.getByRole("button", { name: "Open filters" });
    opener.focus();
    rerender(renderDrawer(true));

    const first = screen.getByRole("button", { name: "Close filters" });
    await waitFor(() => expect(first).toHaveFocus());

    screen.getByRole("button", { name: "Close", exact: true }).focus();
    fireEvent.keyDown(document, { key: "Tab" });
    expect(first).toHaveFocus();

    rerender(renderDrawer(false));
    expect(opener).toHaveFocus();
  });

  it("enables and triggers Clear Filters when hasActiveFilters is true", () => {
    const handleClearFilters = vi.fn();
    render(
      <CardFiltersDrawer
        open={true}
        onOpenChange={vi.fn()}
        filters={{ ...defaultFilters, sets: ["SOR"] }}
        setFilters={vi.fn()}
        availableFacets={defaultFacets}
        isFacetsLoading={false}
        isFacetsError={false}
        onRetryFacets={vi.fn()}
        onClearFilters={handleClearFilters}
        hasActiveFilters={true}
      />
    );

    const clearButton = screen.getByRole("button", { name: "Clear Filters" });
    expect(clearButton).toBeEnabled();
    fireEvent.click(clearButton);
    expect(handleClearFilters).toHaveBeenCalledOnce();
  });

  it("displays loading and error states for facets", () => {
    const handleRetry = vi.fn();
    const { rerender } = render(
      <CardFiltersDrawer
        open={true}
        onOpenChange={vi.fn()}
        filters={defaultFilters}
        setFilters={vi.fn()}
        availableFacets={defaultFacets}
        isFacetsLoading={true}
        isFacetsError={false}
        onRetryFacets={handleRetry}
        onClearFilters={vi.fn()}
        hasActiveFilters={false}
      />
    );

    expect(screen.getByText("Loading filters…")).toBeInTheDocument();

    rerender(
      <CardFiltersDrawer
        open={true}
        onOpenChange={vi.fn()}
        filters={defaultFilters}
        setFilters={vi.fn()}
        availableFacets={defaultFacets}
        isFacetsLoading={false}
        isFacetsError={true}
        onRetryFacets={handleRetry}
        onClearFilters={vi.fn()}
        hasActiveFilters={false}
      />
    );

    expect(screen.getByText("Unable to load filters.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Retry filters" }));
    expect(handleRetry).toHaveBeenCalledOnce();
  });
  it("renders SWCCG filters (Dark/Light toggle, Type, Persona, Set) and hides SWU facets when game is Star Wars CCG", () => {
    render(
      <CardFiltersDrawer
        open={true}
        onOpenChange={vi.fn()}
        filters={{ ...defaultFilters, side: "Dark" }}
        setFilters={vi.fn()}
        availableFacets={{
          ...defaultFacets,
          personas: ["Vader", "Luke"],
        }}
        isFacetsLoading={false}
        isFacetsError={false}
        onRetryFacets={vi.fn()}
        onClearFilters={vi.fn()}
        hasActiveFilters={false}
        game="Star Wars CCG"
      />
    );

    // SWCCG specific controls
    const darkBtn = screen.getByRole("button", { name: "Dark Side" });
    const lightBtn = screen.getByRole("button", { name: "Light Side" });
    expect(darkBtn).toHaveAttribute("aria-pressed", "true");
    expect(lightBtn).toHaveAttribute("aria-pressed", "false");

    expect(screen.getByRole("button", { name: /^Type/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^Persona/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^Set/i })).toBeInTheDocument();

    // SWU specific facets should not be rendered
    expect(screen.queryByRole("button", { name: /^Aspect/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^Arena/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^Rarity/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^Traits/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^Keywords/i })).not.toBeInTheDocument();
  });

  it("toggles Side to Light Side when Light Side button is clicked", () => {
    const handleSetFilters = vi.fn();
    render(
      <CardFiltersDrawer
        open={true}
        onOpenChange={vi.fn()}
        filters={{ ...defaultFilters, side: "Dark" }}
        setFilters={handleSetFilters}
        availableFacets={defaultFacets}
        isFacetsLoading={false}
        isFacetsError={false}
        onRetryFacets={vi.fn()}
        onClearFilters={vi.fn()}
        hasActiveFilters={false}
        game="Star Wars CCG"
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Light Side" }));
    expect(handleSetFilters).toHaveBeenCalledOnce();
    const updater = handleSetFilters.mock.calls[0][0];
    expect(updater(defaultFilters)).toMatchObject({ side: "Light", page: 1 });
  });
});
