import type { ReactElement } from "react";

export type SwccgUniquenessProps = {
  uniqueness?: string | null;
  side?: string | null;
};

export type ParsedUniqueness = {
  type: "dot" | "diamond";
  count: number;
};

/**
 * Parses a SWCCG uniqueness string (e.g. "*", "**", "<><><>") into its symbol type and count.
 */
export function parseSwccgUniqueness(uniqueness?: string | null): ParsedUniqueness | null {
  if (!uniqueness) return null;
  const trimmed = uniqueness.trim();
  if (!trimmed) return null;

  // Diamonds: "<>", "<><>", "<><><>", or Unicode diamonds
  if (trimmed.includes("<>") || trimmed.includes("◇") || trimmed.includes("◆") || trimmed.includes("◊")) {
    const matches = trimmed.match(/<>|[◇◆◊]/g);
    const count = matches ? matches.length : 0;
    if (count > 0) return { type: "diamond", count };
  }

  // Dots: "*", "**", "***", "•", "·", "●"
  if (trimmed.includes("*") || trimmed.includes("•") || trimmed.includes("·") || trimmed.includes("∙") || trimmed.includes("●")) {
    const matches = trimmed.match(/[*•·∙●]/g);
    const count = matches ? matches.length : 0;
    if (count > 0) return { type: "dot", count };
  }

  return null;
}

/**
 * Renders SWCCG card uniqueness symbols (dots or diamonds) to the left of the card title.
 */
export function SwccgUniqueness({ uniqueness, side }: SwccgUniquenessProps): ReactElement | null {
  const parsed = parseSwccgUniqueness(uniqueness);
  if (!parsed || parsed.count <= 0) return null;

  if (parsed.type === "dot") {
    return (
      <span
        data-testid="swccg-uniqueness-dots"
        aria-label={`${parsed.count} dot uniqueness`}
        className="inline-block text-foreground tracking-tighter select-none mr-0.5 align-baseline"
      >
        {"•".repeat(parsed.count)}
      </span>
    );
  }

  const isDark = side?.toLowerCase().includes("dark");
  const isLight = side?.toLowerCase().includes("light");
  const diamondColor = isDark
    ? "text-red-500"
    : isLight
      ? "text-blue-400"
      : "text-amber-400";

  return (
    <span
      data-testid="swccg-uniqueness-diamonds"
      aria-label={`${parsed.count} diamond uniqueness`}
      className="inline-flex items-center gap-0.5 mr-1 align-baseline select-none"
    >
      <span className="sr-only">{"◇".repeat(parsed.count)}</span>
      {Array.from({ length: parsed.count }, (_, i) => (
        <svg
          key={i}
          className={`inline-block h-4 w-4 fill-none stroke-current stroke-[2.5] align-middle -mt-1 ${diamondColor}`}
          viewBox="0 0 24 24"
          aria-hidden="true"
        >
          <polygon points="12,3 21,12 12,21 3,12" />
        </svg>
      ))}
    </span>
  );
}

export default SwccgUniqueness;
