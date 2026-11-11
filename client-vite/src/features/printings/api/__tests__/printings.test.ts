import { beforeEach, describe, expect, it, vi } from "vitest";

const { getMock } = vi.hoisted(() => ({
  getMock: vi.fn(),
}));

vi.mock("@/lib/api", () => ({
  api: { get: getMock },
}));

import { api } from "@/lib/api";
import { fetchPrintings } from "../printings";

describe("fetchPrintings", () => {
  beforeEach(() => {
    getMock.mockReset().mockResolvedValue({ data: [], headers: {} });
  });

  it("serializes selected Pokemon HP values as the hp query parameter", async () => {
    await fetchPrintings({ game: ["Pokemon TCG"], hps: ["100"] });

    expect(api.get).toHaveBeenCalledWith(expect.stringContaining("hp=100"));
  });

  it("serializes selected stages as the stage query parameter", async () => {
    await fetchPrintings({ stages: ["Stage 1"] });

    expect(api.get).toHaveBeenCalledWith(expect.stringContaining("stage=Stage+1"));
  });
});
