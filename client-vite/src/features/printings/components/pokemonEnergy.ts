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

export const POKEMON_ENERGY_CONFIG: Record<
  string,
  { name: PokemonEnergyType; bg: string; text: string; border: string; symbol: string }
> = {
  grass: {
    name: "Grass",
    bg: "bg-[#5bb839]",
    text: "text-white",
    border: "border-[#48992b]",
    symbol: "🍃",
  },
  fire: {
    name: "Fire",
    bg: "bg-[#f05a28]",
    text: "text-white",
    border: "border-[#cf4214]",
    symbol: "🔥",
  },
  water: {
    name: "Water",
    bg: "bg-[#2892df]",
    text: "text-white",
    border: "border-[#1d76b7]",
    symbol: "💧",
  },
  lightning: {
    name: "Lightning",
    bg: "bg-[#fac31d]",
    text: "text-slate-900",
    border: "border-[#d9a30b]",
    symbol: "⚡",
  },
  psychic: {
    name: "Psychic",
    bg: "bg-[#8b559b]",
    text: "text-white",
    border: "border-[#6d3e7b]",
    symbol: "👁️",
  },
  fighting: {
    name: "Fighting",
    bg: "bg-[#c85526]",
    text: "text-white",
    border: "border-[#a23d14]",
    symbol: "👊",
  },
  darkness: {
    name: "Darkness",
    bg: "bg-[#2d4957]",
    text: "text-white",
    border: "border-[#1c323d]",
    symbol: "🌙",
  },
  metal: {
    name: "Metal",
    bg: "bg-[#9da7b0]",
    text: "text-slate-900",
    border: "border-[#7e8890]",
    symbol: "⚙️",
  },
  colorless: {
    name: "Colorless",
    bg: "bg-[#e5e5dc]",
    text: "text-slate-800",
    border: "border-[#c4c4b8]",
    symbol: "⭐",
  },
  dragon: {
    name: "Dragon",
    bg: "bg-[#c6a12b]",
    text: "text-white",
    border: "border-[#9c7d1c]",
    symbol: "🐉",
  },
  fairy: {
    name: "Fairy",
    bg: "bg-[#f278a1]",
    text: "text-white",
    border: "border-[#d85581]",
    symbol: "✨",
  },
};
