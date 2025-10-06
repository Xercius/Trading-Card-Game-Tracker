import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

describe("http module", () => {
  it("imports in Node without window/localStorage", async () => {
    const originalWindow = (globalThis as { window?: unknown }).window;
    // Simulate Node-like environment
    // eslint-disable-next-line @typescript-eslint/no-dynamic-delete
    delete (globalThis as { window?: unknown }).window;
    vi.resetModules();
    const mod = await import("./http");
    expect(typeof mod.default).toBe("function");
    expect(typeof mod.setHttpUserId).toBe("function");
    (globalThis as { window?: unknown }).window = originalWindow;
    vi.resetModules();
  });
});

describe("API_ORIGIN determination", () => {
  let originalLocation: Location;

  beforeEach(() => {
    originalLocation = window.location;
    // Mock location.origin
    Object.defineProperty(window, "location", {
      value: { origin: "http://localhost:5173" },
      writable: true,
      configurable: true,
    });
  });

  afterEach(() => {
    Object.defineProperty(window, "location", {
      value: originalLocation,
      writable: true,
      configurable: true,
    });
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it("uses location.origin when VITE_API_BASE is a relative path", async () => {
    vi.stubEnv("VITE_API_BASE", "/api");
    vi.resetModules();
    const { resolveImageUrl } = await import("./http");

    const result = resolveImageUrl("images/card/123.png");
    expect(result).toBe("http://localhost:5173/images/card/123.png");
  });

  it("extracts origin from VITE_API_BASE when it's an absolute URL", async () => {
    vi.stubEnv("VITE_API_BASE", "https://api.example.com/api");
    vi.resetModules();
    const { resolveImageUrl } = await import("./http");

    const result = resolveImageUrl("images/card/123.png");
    expect(result).toBe("https://api.example.com/images/card/123.png");
  });

  it("uses location.origin when VITE_API_BASE is not set (defaults to /api)", async () => {
    vi.stubEnv("VITE_API_BASE", undefined);
    vi.resetModules();
    const { resolveImageUrl } = await import("./http");

    const result = resolveImageUrl("images/card/123.png");
    expect(result).toBe("http://localhost:5173/images/card/123.png");
  });

  it("handles absolute HTTP URLs in VITE_API_BASE case-insensitively", async () => {
    vi.stubEnv("VITE_API_BASE", "HTTP://API.EXAMPLE.COM/api");
    vi.resetModules();
    const { resolveImageUrl } = await import("./http");

    const result = resolveImageUrl("images/card/123.png");
    expect(result).toBe("http://api.example.com/images/card/123.png");
  });

  it("handles HTTPS URLs in VITE_API_BASE", async () => {
    vi.stubEnv("VITE_API_BASE", "https://secure-api.example.com/api");
    vi.resetModules();
    const { resolveImageUrl } = await import("./http");

    const result = resolveImageUrl("images/card/123.png");
    expect(result).toBe("https://secure-api.example.com/images/card/123.png");
  });

  it("returns path unchanged when it's already an absolute URL", async () => {
    vi.stubEnv("VITE_API_BASE", "/api");
    vi.resetModules();
    const { resolveImageUrl } = await import("./http");

    const result = resolveImageUrl("https://cdn.example.com/card.png");
    expect(result).toBe("https://cdn.example.com/card.png");
  });
});
