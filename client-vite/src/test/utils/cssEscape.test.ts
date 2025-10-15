import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { cssEscapeId } from "./cssEscape";

describe("cssEscapeId", () => {
  let originalCSSEscape: typeof CSS.escape | undefined;

  beforeEach(() => {
    // Store original CSS.escape
    originalCSSEscape = (globalThis as any).CSS?.escape;
  });

  afterEach(() => {
    // Restore original CSS.escape
    if (originalCSSEscape) {
      if (!(globalThis as any).CSS) {
        (globalThis as any).CSS = {};
      }
      (globalThis as any).CSS.escape = originalCSSEscape;
    } else {
      delete (globalThis as any).CSS?.escape;
    }
  });

  it("returns the ID unchanged when no special characters are present", () => {
    expect(cssEscapeId("simple-id")).toBe("simple-id");
  });

  it("uses native CSS.escape when available", () => {
    // CSS.escape is available in jsdom
    const result = cssEscapeId("item:123");
    // Native CSS.escape escapes colons
    expect(result).toContain("\\:");
  });

  it("escapes colons using fallback when CSS.escape is not available", () => {
    // Remove CSS.escape to test fallback
    delete (globalThis as any).CSS?.escape;
    const result = cssEscapeId("item:123");
    expect(result).toBe("item\\:123");
  });

  it("escapes leading digits using fallback when CSS.escape is not available", () => {
    // Remove CSS.escape to test fallback
    delete (globalThis as any).CSS?.escape;
    const result = cssEscapeId("123abc");
    // Fallback escapes the first digit: \31 23abc
    expect(result).toMatch(/^\\3[0-9]/);
  });

  it("handles IDs with both leading digits and colons using fallback", () => {
    // Remove CSS.escape to test fallback
    delete (globalThis as any).CSS?.escape;
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
