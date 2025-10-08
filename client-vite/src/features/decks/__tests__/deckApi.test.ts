import { afterEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { postDeckQuantityDelta } from "../api";

describe("deck API helpers", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("posts quantity deltas to the dedicated endpoint", async () => {
    const spy = vi.spyOn(api, "post").mockResolvedValue({} as Awaited<ReturnType<typeof api.post>>);

    await postDeckQuantityDelta(7, true, { printingId: 42, qtyDelta: 3 });

    expect(spy).toHaveBeenCalledWith(
      "decks/7/cards/quantity-delta",
      { printingId: 42, qtyDelta: 3 },
      { params: { includeProxies: true } }
    );
  });
});
