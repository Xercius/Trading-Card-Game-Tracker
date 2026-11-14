import { Fragment } from "react";

const MTG_SYMBOL_TOKEN_REGEX = /(\{[^}]+\})/g;

function normalizeScryfallSymbolCode(rawToken: string): string {
  const inner = rawToken.startsWith("{") && rawToken.endsWith("}") ? rawToken.slice(1, -1) : rawToken;
  return inner.trim().replace(/\//g, "").toUpperCase();
}

const SYMBOL_ALT_LABELS: Record<string, string> = {
  W: "White mana",
  U: "Blue mana",
  B: "Black mana",
  R: "Red mana",
  G: "Green mana (Forest)",
  C: "Colorless mana",
  T: "Tap",
  Q: "Untap",
  X: "X mana",
  S: "Snow mana",
  E: "Energy",
};

/**
 * Renders a single Scryfall mana or ability symbol SVG (e.g., "{G}", "{T}", "{2}").
 */
export function MtgSymbolIcon({
  symbol,
  size = 16,
  className = "inline-block align-text-bottom",
}: {
  symbol: string;
  size?: number;
  className?: string;
}) {
  const code = normalizeScryfallSymbolCode(symbol);
  if (!code) return null;

  const label = SYMBOL_ALT_LABELS[code] ?? symbol;
  const src = `https://svgs.scryfall.io/card-symbols/${encodeURIComponent(code)}.svg`;

  return (
    <img
      src={src}
      alt={label}
      title={label}
      width={size}
      height={size}
      loading="lazy"
      decoding="async"
      className={`shrink-0 select-none ${className}`}
      style={{ width: `${size}px`, height: `${size}px` }}
    />
  );
}

/**
 * Parses a Scryfall mana cost string (e.g., "{2}{G}{G}") into inline symbol icons.
 */
export function MtgManaCost({
  manaCost,
  size = 16,
}: {
  manaCost: string | null | undefined;
  size?: number;
}) {
  if (!manaCost || !manaCost.trim()) return null;

  const tokens = manaCost.match(MTG_SYMBOL_TOKEN_REGEX);
  if (!tokens || tokens.length === 0) {
    return <span className="text-xs font-mono">{manaCost}</span>;
  }

  return (
    <span className="inline-flex items-center gap-0.5" aria-label={`Mana cost ${manaCost}`}>
      {tokens.map((token, idx) => (
        <MtgSymbolIcon key={`${token}-${idx}`} symbol={token} size={size} />
      ))}
    </span>
  );
}

/**
 * Renders MTG oracle rules text, replacing "{...}" tokens with inline Scryfall SVG icons.
 */
export function MtgCardText({ text }: { text: string | null | undefined }) {
  if (!text || !text.trim()) {
    return <span className="text-sm italic text-muted-foreground">No card text available.</span>;
  }

  const lines = text.split("\n");

  return (
    <div className="space-y-2 text-sm leading-relaxed text-foreground/90">
      {lines.map((line, lineIdx) => {
        const trimmed = line.trim();
        if (!trimmed) return null;

        const parts = trimmed.split(MTG_SYMBOL_TOKEN_REGEX);
        return (
          <p key={`mtg-line-${lineIdx}`} className="leading-relaxed">
            {parts.map((part, partIdx) => {
              if (!part) return null;
              if (part.startsWith("{") && part.endsWith("}")) {
                return (
                  <Fragment key={`sym-${lineIdx}-${partIdx}`}>
                    <MtgSymbolIcon symbol={part} size={15} className="inline-block mx-0.5 align-text-bottom" />
                  </Fragment>
                );
              }
              return <Fragment key={`txt-${lineIdx}-${partIdx}`}>{part}</Fragment>;
            })}
          </p>
        );
      })}
    </div>
  );
}
