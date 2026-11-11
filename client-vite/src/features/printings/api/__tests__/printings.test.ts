import { beforeEach, describe, expect, it, vi } from "vitest";

const { getMock } = vi.hoisted(() => ({
  getMock: vi.fn().mockResolvedValue({ data: [], headers: { "x-total-count": "0" } }),
}));

vi.mock("@/lib/api", () => ({
  api: { get: getMock },
}));

import { api } from "@/lib/api";
import { fetchPrintings } from "../printings";

describe("fetchPrintings", () => {
  beforeEach(() => {
    getMock.mockClear();
  });

  it("serializes selected Pokemon HP values as the hp query parameter", async () => {
    await fetchPrintings({ game: ["Pokemon TCG"], hps: ["100"] });

    expect(api.get).toHaveBeenCalledWith(expect.stringContaining("hp=100"));
  });
});
