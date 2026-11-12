export type PokemonEnergyType =
  | "Grass"
  | "Fire"
  | "Water"
  | "Lightning"
  | "Psychic"
  | "Fighting"
  | "Darkness"
  | "Metal"
  | "Colorless"
  | "Dragon"
  | "Fairy";

export type PokemonEnergyConfigEntry = {
  name: PokemonEnergyType;
  imagePath: string;
};

export const POKEMON_ENERGY_CONFIG: Record<string, PokemonEnergyConfigEntry> = {
  grass: {
    name: "Grass",
    imagePath: "/images/games/pokemon/pokemontypes/grass_type_symbol.png",
  },
  fire: {
    name: "Fire",
    imagePath: "/images/games/pokemon/pokemontypes/fire_type_symbol.png",
  },
  water: {
    name: "Water",
    imagePath: "/images/games/pokemon/pokemontypes/water_type_symbol.png",
  },
  lightning: {
    name: "Lightning",
    imagePath: "/images/games/pokemon/pokemontypes/lightning_type_symbol.png",
  },
  psychic: {
    name: "Psychic",
    imagePath: "/images/games/pokemon/pokemontypes/psychic_type_symbol.png",
  },
  fighting: {
    name: "Fighting",
    imagePath: "/images/games/pokemon/pokemontypes/fighting_type_symbol.png",
  },
  darkness: {
    name: "Darkness",
    imagePath: "/images/games/pokemon/pokemontypes/darkness_type_symbol.png",
  },
  metal: {
    name: "Metal",
    imagePath: "/images/games/pokemon/pokemontypes/metal_type_symbol.png",
  },
  colorless: {
    name: "Colorless",
    imagePath: "/images/games/pokemon/pokemontypes/colorless_type_symbol.png",
  },
  dragon: {
    name: "Dragon",
    imagePath: "/images/games/pokemon/pokemontypes/dragon_type_symbol.png",
  },
  fairy: {
    name: "Fairy",
    imagePath: "/images/games/pokemon/pokemontypes/fairy_type_symbol.png",
  },
};
