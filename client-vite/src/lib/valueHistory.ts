import type { ValuePoint } from "@/types/value";

export function latestValue(points: ValuePoint[]): number | null {
  for (let index = points.length - 1; index >= 0; index -= 1) {
    const value = points[index]?.v;
    if (value != null) return value;
  }
  return null;
}
