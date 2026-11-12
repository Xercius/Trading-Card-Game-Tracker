import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PokemonEnergyIcon, PokemonCardText } from "../PokemonEnergyIcon";

describe("PokemonEnergyIcon", () => {
  it("renders standard energy icons using official type images", () => {
    render(<PokemonEnergyIcon type="Fire" />);
    const icon = screen.getByRole("img", { name: "Fire Energy" });
    expect(icon).toBeInTheDocument();
    expect(icon).toHaveAttribute(
      "src",
      expect.stringContaining("/images/games/pokemon/pokemontypes/fire_type_symbol.png")
    );
  });

  it("handles case-insensitivity and fallback for unknown types", () => {
    render(<PokemonEnergyIcon type="psychic" />);
    const psychicIcon = screen.getByRole("img", { name: "Psychic Energy" });
    expect(psychicIcon).toBeInTheDocument();
    expect(psychicIcon).toHaveAttribute(
      "src",
      expect.stringContaining("/images/games/pokemon/pokemontypes/psychic_type_symbol.png")
    );

    render(<PokemonEnergyIcon type="CustomType" />);
    expect(screen.getByText("CustomType")).toBeInTheDocument();
  });
});

describe("PokemonCardText", () => {
  it("renders energy tokens inline and ability badges", () => {
    render(<PokemonCardText text="[Ability] Quick Attack: [Colorless][Colorless] Slash (80)" />);
    expect(screen.getByText("Ability")).toBeInTheDocument();
    expect(screen.getByText(/Quick Attack:/)).toBeInTheDocument();
    const colorlessIcons = screen.getAllByRole("img", { name: "Colorless Energy" });
    expect(colorlessIcons).toHaveLength(2);
    expect(colorlessIcons[0]).toHaveAttribute(
      "src",
      expect.stringContaining("/images/games/pokemon/pokemontypes/colorless_type_symbol.png")
    );
    expect(screen.getByText(/Slash \(80\)/)).toBeInTheDocument();
  });

  it("renders fallback when text is empty", () => {
    render(<PokemonCardText text="" />);
    expect(screen.getByText("No card text available.")).toBeInTheDocument();
  });
});
