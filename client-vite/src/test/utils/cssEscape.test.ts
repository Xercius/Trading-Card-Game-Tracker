import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { cssEscapeId } from "./cssEscape";

type CssGlobal = { escape?: (selector: string) => string };

function getCssGlobal(): CssGlobal | undefined {
  return Reflect.get(globalThis, "CSS") as CssGlobal | undefined;
}

describe("cssEscapeId", () => {
  let originalCSSEscape: CssGlobal["escape"];

  beforeEach(() => {
    // Store original CSS.escape
    originalCSSEscape = getCssGlobal()?.escape;
  });

  afterEach(() => {
    // Restore original CSS.escape
    if (originalCSSEscape) {
      const css = getCssGlobal();
      if (css) css.escape = originalCSSEscape;
      else Reflect.set(globalThis, "CSS", { escape: originalCSSEscape });
    } else {
      const css = getCssGlobal();
      if (css && "escape" in css) delete css.escape;
    }
  });

  it("returns the ID unchanged when no special characters are present", () => {
    expect(cssEscapeId("simple-id")).toBe("simple-id");
  });

  it("uses native CSS.escape when available", () => {
    // Stub CSS.escape with a deterministic mock
    const mockEscape = vi.fn((selector: string) => `__escaped__${selector}`);
    if (!getCssGlobal()) Reflect.set(globalThis, "CSS", {});
    getCssGlobal()!.escape = mockEscape;
    const result = cssEscapeId("item:123");
    expect(result).toBe("__escaped__item:123");
    expect(mockEscape).toHaveBeenCalledWith("item:123");
  });

  it("escapes colons using fallback when CSS.escape is not available", () => {
    // Remove CSS.escape to test fallback
    const css = getCssGlobal();
    if (css && "escape" in css) delete css.escape;
    const result = cssEscapeId("item:123");
    expect(result).toBe("item\\:123");
  });

  it("escapes leading digits using fallback when CSS.escape is not available", () => {
    // Remove CSS.escape to test fallback
    const css = getCssGlobal();
    if (css && "escape" in css) delete css.escape;
    const result = cssEscapeId("123abc");
    // Fallback escapes the first digit: \31 23abc
    expect(result).toMatch(/^\\3[0-9]/);
  });

  it("handles IDs with both leading digits and colons using fallback", () => {
    // Remove CSS.escape to test fallback
    const css = getCssGlobal();
    if (css && "escape" in css) delete css.escape;
    const result = cssEscapeId("5item:123");
    // Fallback escapes leading digit and colons
    expect(result).toMatch(/^\\3[0-9]/);
    expect(result).toContain("\\:");
  });

  it("works with common test IDs", () => {
    expect(cssEscapeId("login-username")).toBe("login-username");
    expect(cssEscapeId("login-password")).toBe("login-password");
  });

  it("can be used in querySelector", () => {
    // Create a test element
    const div = document.createElement("div");
    div.id = "test:item";
    document.body.appendChild(div);

    try {
      // This should not throw an error
      const el = document.querySelector(`#${cssEscapeId("test:item")}`);
      expect(el).toBe(div);
    } finally {
      div.remove();
    }
  });
});
