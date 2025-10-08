import type { ValuePoint } from "@/types/value";
import { buildSparklinePath } from "@/features/cards/utils/sparkline";

type Props = {
  points: ValuePoint[];
  ariaLabel: string;
  height?: number;
  stroke?: string;
  className?: string;
  emptyLabel?: string;
};

export default function LineSparkline({
  points,
  ariaLabel,
  height = 96,
  stroke = "hsl(var(--primary))",
  className,
  emptyLabel = "No data",
}: Props) {
  const path = buildSparklinePath(points);

  if (!path) {
    const classes = ["flex items-center justify-center text-xs text-muted-foreground", className]
      .filter(Boolean)
      .join(" ");
    return (
      <div role="img" aria-label={`${ariaLabel} (no data)`} className={classes} style={{ height }}>
        {emptyLabel}
      </div>
    );
  }

  const svgClass = ["w-full", className].filter(Boolean).join(" ");

  return (
    <svg
      role="img"
      aria-label={ariaLabel}
      viewBox="0 0 100 100"
      preserveAspectRatio="none"
      className={svgClass}
      style={{ height }}
    >
      <path
        d={path}
        fill="none"
        stroke={stroke}
        strokeWidth={2}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
