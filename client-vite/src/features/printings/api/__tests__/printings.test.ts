import { beforeEach, describe, expect, it, vi } from "vitest";

const { apiGetMock } = vi.hoisted(() => ({
  apiGetMock: vi.fn(),
}));

vi.mock("@/lib/api", () => ({
  api: { get: apiGetMock },
}));

import { fetchPrintings } from "../printings";

describe("fetchPrintings", () => {
  beforeEach(() => {
    apiGetMock.mockReset().mockResolvedValue({ data: [], headers: {} });
  });

  it("serializes selected stages as the stage query parameter", async () => {
    await fetchPrintings({ stages: ["Stage 1"] });

    expect(apiGetMock).toHaveBeenCalledWith(expect.stringContaining("stage=Stage+1"));
  });
});
