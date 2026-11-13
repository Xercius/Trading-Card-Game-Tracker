import { beforeEach, describe, expect, it, vi } from "vitest";

const { getMock } = vi.hoisted(() => ({
  getMock: vi.fn(),
}));

vi.mock("@/lib/http", () => ({
  default: { get: getMock },
}));

import { fetchCardsPage } from "../list";

describe("fetchCardsPage", () => {
  beforeEach(() => {
    getMock.mockReset();
  });

  it("normalizes the primary set logo from camel-case and Pascal-case card data", async () => {
    getMock.mockResolvedValue({
      data: {
        items: [
          { primary: { setLogoUrl: "https://example.com/camel.webp" } },
          { Primary: { SetLogoUrl: "https://example.com/pascal.webp" } },
        ],
      },
    });

    const page = await fetchCardsPage({ skip: 0, take: 10 });

    expect(page.items.map(({ setLogoUrl }) => setLogoUrl)).toEqual([
      "https://example.com/camel.webp",
      "https://example.com/pascal.webp",
    ]);
  });
});
