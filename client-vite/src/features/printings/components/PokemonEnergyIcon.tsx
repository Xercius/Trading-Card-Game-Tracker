import { POKEMON_ENERGY_CONFIG } from "./pokemonEnergy";

export function PokemonEnergyIcon({
  type,
  className = "inline-flex h-4 w-4",
  size = 16,
}: {
  type: string;
  className?: string;
  size?: number;
}) {
  const normalized = type.toLowerCase().trim();
  const config = POKEMON_ENERGY_CONFIG[normalized];

  if (!config) {
    return (
      <span
        className={`inline-flex items-center justify-center rounded-full bg-muted text-[10px] font-bold px-1 ${className}`}
        title={type}
      >
        {type}
      </span>
    );
  }

  return (
    <span
      className={`relative inline-flex items-center justify-center rounded-full font-bold shadow-sm select-none border shrink-0 ${config.bg} ${config.text} ${config.border} ${className}`}
      style={{
        width: `${size}px`,
        height: `${size}px`,
        minWidth: `${size}px`,
        minHeight: `${size}px`,
      }}
      title={`${config.name} Energy`}
      aria-label={`${config.name} Energy`}
    >
      <span
        style={{ fontSize: `${Math.round(size * 0.65)}px`, lineHeight: 1 }}
        className="pointer-events-none select-none flex items-center justify-center"
      >
        {config.symbol}
      </span>
    </span>
  );
}

const TOKEN_REGEX =
  /(\[(?:Grass|Fire|Water|Lightning|Psychic|Fighting|Darkness|Metal|Colorless|Dragon|Fairy|Ability|Poké-Body|Poke-Body|Poké-Power|Poke-Power)\])/gi;

export function PokemonCardText({ text }: { text: string | null | undefined }) {
  if (!text || !text.trim()) {
    return <span className="text-sm italic text-muted-foreground">No card text available.</span>;
  }

  const lines = text.split("\n");

  return (
    <div className="space-y-2 text-sm leading-relaxed text-foreground/90">
      {lines.map((line, lineIdx) => {
        const trimmed = line.trim();
        if (!trimmed) return null;

        const parts = trimmed.split(TOKEN_REGEX);

        return (
          <p key={`line-${lineIdx}`} className="flex flex-wrap items-baseline gap-x-1">
            {parts.map((part, partIdx) => {
              if (!part) return null;

              if (part.startsWith("[") && part.endsWith("]")) {
                const token = part.slice(1, -1);
                const tokenLower = token.toLowerCase();

                if (
                  tokenLower === "ability" ||
                  tokenLower === "poké-body" ||
                  tokenLower === "poke-body" ||
                  tokenLower === "poké-power" ||
                  tokenLower === "poke-power"
                ) {
                  return (
                    <span
                      key={`part-${partIdx}`}
                      className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-extrabold uppercase tracking-wider bg-rose-600 text-white mr-1 shadow-xs"
                    >
                      {token}
                    </span>
                  );
                }

                if (POKEMON_ENERGY_CONFIG[tokenLower]) {
                  return (
                    <PokemonEnergyIcon
                      key={`part-${partIdx}`}
                      type={token}
                      size={18}
                      className="inline-flex mx-0.5 align-middle -translate-y-0.5"
                    />
                  );
                }
              }

              return <span key={`part-${partIdx}`}>{part}</span>;
            })}
          </p>
        );
      })}
    </div>
  );
}
