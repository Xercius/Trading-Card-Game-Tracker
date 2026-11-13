import { beforeEach, describe, expect, it, vi } from "vitest";

const { getMock } = vi.hoisted(() => ({
  getMock: vi.fn(),
}));

vi.mock("@/lib/http", () => ({
  default: { get: getMock },
}));

import http from "@/lib/http";
import { fetchPrintings } from "../printings";

describe("fetchPrintings", () => {
  beforeEach(() => {
    getMock.mockReset().mockResolvedValue({ data: [], headers: {} });
  });

  it("serializes selected Pokemon HP values as the hp query parameter", async () => {
    await fetchPrintings({ game: ["Pokemon TCG"], hps: ["100"] });

    expect(http.get).toHaveBeenCalledWith(expect.stringContaining("hp=100"));
  });

  it("serializes selected stages as the stage query parameter", async () => {
    await fetchPrintings({ stages: ["Stage 1"] });

    expect(http.get).toHaveBeenCalledWith(expect.stringContaining("stage=Stage+1"));
  });
});
