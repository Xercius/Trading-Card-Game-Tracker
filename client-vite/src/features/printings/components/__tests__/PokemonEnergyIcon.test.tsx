import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PokemonEnergyIcon, PokemonCardText } from "../PokemonEnergyIcon";

describe("PokemonEnergyIcon", () => {
  it("renders standard energy icons", () => {
    render(<PokemonEnergyIcon type="Fire" />);
    expect(screen.getByLabelText("Fire Energy")).toBeInTheDocument();
  });

  it("handles case-insensitivity and fallback for unknown types", () => {
    render(<PokemonEnergyIcon type="psychic" />);
    expect(screen.getByLabelText("Psychic Energy")).toBeInTheDocument();

    render(<PokemonEnergyIcon type="CustomType" />);
    expect(screen.getByText("CustomType")).toBeInTheDocument();
  });
});

describe("PokemonCardText", () => {
  it("renders energy tokens inline and ability badges", () => {
    render(<PokemonCardText text="[Ability] Quick Attack: [Colorless][Colorless] Slash (80)" />);
    expect(screen.getByText("Ability")).toBeInTheDocument();
    expect(screen.getByText(/Quick Attack:/)).toBeInTheDocument();
    const colorlessIcons = screen.getAllByLabelText("Colorless Energy");
    expect(colorlessIcons).toHaveLength(2);
    expect(screen.getByText(/Slash \(80\)/)).toBeInTheDocument();
  });

  it("renders fallback when text is empty", () => {
    render(<PokemonCardText text="" />);
    expect(screen.getByText("No card text available.")).toBeInTheDocument();
  });
});
