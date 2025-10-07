import type { ValuePoint } from "@/types/value";

export function buildSparklinePath(points: ValuePoint[]): string | null {
  if (points.length === 0) return null;

  const validPoints = points.filter((point) => point.v != null);
  if (validPoints.length === 0) return null;

  const values = validPoints.map((point) => point.v ?? 0);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min || 1;

  return validPoints
    .map((point, index) => {
      const x = validPoints.length === 1 ? 0 : (index / (validPoints.length - 1)) * 100;
      const y = 100 - (((point.v ?? 0) - min) / range) * 100;
      return `${index === 0 ? "M" : "L"}${x.toFixed(2)},${y.toFixed(2)}`;
    })
    .join(" ");
}
