import { describe, expect, it } from "vitest";
import { buildSparklinePath } from "../sparkline";
import type { ValuePoint } from "@/types/value";

describe("buildSparklinePath", () => {
  it("returns horizontal line for flat price history", () => {
    const points: ValuePoint[] = [
      { d: "2024-01-01", v: 5 },
      { d: "2024-01-02", v: 5 },
      { d: "2024-01-03", v: 5 },
    ];

    expect(buildSparklinePath(points)).toBe("M0.00,100.00 L50.00,100.00 L100.00,100.00");
  });

  it("returns ascending path for increasing prices", () => {
    const points: ValuePoint[] = [
      { d: "2024-01-01", v: 1 },
      { d: "2024-01-02", v: 2 },
      { d: "2024-01-03", v: 3 },
    ];

    expect(buildSparklinePath(points)).toBe("M0.00,100.00 L50.00,50.00 L100.00,0.00");
  });

  it("handles single price point", () => {
    const points: ValuePoint[] = [{ d: "2024-01-01", v: 5 }];

    expect(buildSparklinePath(points)).toBe("M0.00,100.00");
  });

  it("skips null values", () => {
    const points: ValuePoint[] = [
      { d: "2024-01-01", v: null },
      { d: "2024-01-02", v: 3 },
      { d: "2024-01-03", v: 6 },
    ];

    expect(buildSparklinePath(points)).toBe("M0.00,100.00 L100.00,0.00");
  });

  it("returns null when all values are null", () => {
    const points: ValuePoint[] = [
      { d: "2024-01-01", v: null },
      { d: "2024-01-02", v: null },
    ];

    expect(buildSparklinePath(points)).toBeNull();
  });

  it("returns null for empty list", () => {
    expect(buildSparklinePath([])).toBeNull();
  });
});
