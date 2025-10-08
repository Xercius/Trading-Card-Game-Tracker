import { describe, expect, it, vi, afterEach } from "vitest";
import { api } from "@/lib/api";
import { ProblemDetailsError } from "@/lib/problemDetails";
import { fetchCollection, type CollectionQueryParams } from "../api";

const baseParams: CollectionQueryParams = {
  userId: 1,
  page: 1,
  pageSize: 50,
  filters: {},
  includeProxies: false,
};

afterEach(() => {
  vi.restoreAllMocks();
});

describe("collection api", () => {
  it("wraps validation errors in ProblemDetailsError", async () => {
    vi.spyOn(api, "get").mockRejectedValue({
      isAxiosError: true,
      toJSON: () => ({}),
      message: "Bad Request",
      response: {
        status: 400,
        data: {
          errors: {
            page: ["Page must be at least 1."],
          },
        },
      },
    });

    const error = await fetchCollection(baseParams).catch((err) => err);
    expect(error).toBeInstanceOf(ProblemDetailsError);
    expect(error).toMatchObject({
      message: "Page must be at least 1.",
      status: 400,
    });
  });
});
