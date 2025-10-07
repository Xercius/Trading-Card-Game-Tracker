import { describe, expect, it } from "vitest";
import { buildSparklinePath } from "../sparkline";
import type { PricePoint } from "../../api";

describe("buildSparklinePath", () => {
  it("returns horizontal line for flat price history", () => {
    const points: PricePoint[] = [
      { d: "2024-01-01", p: 5 },
      { d: "2024-01-02", p: 5 },
      { d: "2024-01-03", p: 5 },
    ];

    expect(buildSparklinePath(points)).toBe("M0.00,100.00 L50.00,100.00 L100.00,100.00");
  });

  it("returns ascending path for increasing prices", () => {
    const points: PricePoint[] = [
      { d: "2024-01-01", p: 1 },
      { d: "2024-01-02", p: 2 },
      { d: "2024-01-03", p: 3 },
    ];

    expect(buildSparklinePath(points)).toBe("M0.00,100.00 L50.00,50.00 L100.00,0.00");
  });

  it("handles single price point", () => {
    const points: PricePoint[] = [{ d: "2024-01-01", p: 5 }];

    expect(buildSparklinePath(points)).toBe("M0.00,100.00");
  });

  it("returns null for empty list", () => {
    expect(buildSparklinePath([])).toBeNull();
  });
});
