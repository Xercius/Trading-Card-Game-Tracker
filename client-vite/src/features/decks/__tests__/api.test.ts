import { describe, expect, it, vi, afterEach } from "vitest";
import { api } from "@/lib/api";
import { ProblemDetailsError } from "@/lib/problemDetails";
import { fetchDeckDetails, postDeckQuantityDelta } from "../api";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("deck api", () => {
  it("returns friendly message for missing decks", async () => {
    vi.spyOn(api, "get").mockRejectedValue({
      isAxiosError: true,
      toJSON: () => ({}),
      message: "Not Found",
      response: {
        status: 404,
        data: {
          detail: "Deck not found.",
        },
      },
    });

    const error = await fetchDeckDetails(42).catch((err) => err);
    expect(error).toBeInstanceOf(ProblemDetailsError);
    expect(error).toMatchObject({
      message: "Deck not found.",
      status: 404,
    });
  });

  it("surfaces conflicts when updating deck quantities", async () => {
    vi.spyOn(api, "post").mockRejectedValue({
      isAxiosError: true,
      toJSON: () => ({}),
      message: "Conflict",
      response: {
        status: 409,
        data: {
          detail: "Not enough copies available.",
        },
      },
    });

    const error = await postDeckQuantityDelta(1, false, { printingId: 7, qtyDelta: 4 }).catch((err) => err);
    expect(error).toBeInstanceOf(ProblemDetailsError);
    expect(error).toMatchObject({
      message: "Not enough copies available.",
      status: 409,
    });
  });
});
