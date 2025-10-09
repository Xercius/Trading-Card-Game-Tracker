import { describe, it, expect, vi } from "vitest";

describe("http module", () => {
  it("imports in Node without window/localStorage", async () => {
    const originalWindow = (globalThis as { window?: unknown }).window;
    // Simulate Node-like environment
    // eslint-disable-next-line @typescript-eslint/no-dynamic-delete
    delete (globalThis as { window?: unknown }).window;
    vi.resetModules();
    const mod = await import("./http");
    expect(typeof mod.default).toBe("function");
    expect(typeof mod.setHttpAccessToken).toBe("function");
    (globalThis as { window?: unknown }).window = originalWindow;
    vi.resetModules();
  });
});
