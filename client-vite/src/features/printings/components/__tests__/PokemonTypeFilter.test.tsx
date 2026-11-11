import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { PokemonTypeFilter } from "../PokemonTypeFilter";

describe("PokemonTypeFilter", () => {
  afterEach(() => {
    cleanup();
  });

  it("allows selecting multiple types without quantity or method controls", () => {
    const onChange = vi.fn();
    const { rerender } = render(<PokemonTypeFilter values={[]} onChange={onChange} />);

    fireEvent.click(screen.getByRole("button", { name: "Types" }));

    expect(screen.getByRole("dialog", { name: "Filter by Pokémon Type" })).toBeInTheDocument();
    expect(screen.queryByText("Filter Method")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Increase|Decrease/ })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Grass" }));
    rerender(<PokemonTypeFilter values={["Grass"]} onChange={onChange} />);
    fireEvent.click(screen.getByRole("button", { name: "Fire" }));

    expect(onChange).toHaveBeenNthCalledWith(1, ["Grass"]);
    expect(onChange).toHaveBeenNthCalledWith(2, ["Grass", "Fire"]);
  });

  it("removes all occurrences of a selected type and deduplicates remaining values", () => {
    const onChange = vi.fn();
    render(<PokemonTypeFilter values={["Grass", "Grass", "Fire", "Fire"]} onChange={onChange} />);

    fireEvent.click(screen.getByRole("button", { name: /Types \(Grass, Fire\)/ }));
    fireEvent.click(screen.getByRole("button", { name: "Grass" }));

    expect(onChange).toHaveBeenCalledWith(["Fire"]);
  });

  it("toggles Colorless / No Type and resets all selected types", () => {
    const onChange = vi.fn();
    render(<PokemonTypeFilter values={["Grass"]} onChange={onChange} />);

    fireEvent.click(screen.getByRole("button", { name: /Types \(Grass\)/ }));
    fireEvent.click(screen.getByRole("button", { name: "Colorless / No Type" }));
    expect(onChange).toHaveBeenCalledWith(["Grass", "no-aspect"]);

    fireEvent.click(screen.getByRole("button", { name: "Reset" }));
    expect(onChange).toHaveBeenLastCalledWith([]);
  });
});
