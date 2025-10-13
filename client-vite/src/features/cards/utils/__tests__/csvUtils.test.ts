import { describe, expect, it } from "vitest";
import { arrayToCsvOrUndefined } from "../csvUtils";

describe("arrayToCsvOrUndefined", () => {
  it("converts an array of strings to a comma-separated string", () => {
    expect(arrayToCsvOrUndefined(["Magic", "Lorcana"])).toBe("Magic,Lorcana");
    expect(arrayToCsvOrUndefined(["R", "U"])).toBe("R,U");
    expect(arrayToCsvOrUndefined(["Rise"])).toBe("Rise");
  });

  it("returns undefined when the array is empty", () => {
    expect(arrayToCsvOrUndefined([])).toBeUndefined();
  });

  it("handles arrays with empty strings", () => {
    expect(arrayToCsvOrUndefined(["", ""])).toBe(",");
  });
});
