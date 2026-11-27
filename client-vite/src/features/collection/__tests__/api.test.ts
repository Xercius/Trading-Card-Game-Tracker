import { describe, expect, it, vi, afterEach } from "vitest";
import { createElement, type ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook } from "@testing-library/react";
import http from "@/lib/http";
import { ProblemDetailsError } from "@/lib/problemDetails";
import {
  fetchCollection,
  useSetCardQuantitiesMutation,
  type CollectionItem,
  type CollectionQueryParams,
} from "../api";

const baseParams: CollectionQueryParams = {
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
    vi.spyOn(http, "get").mockRejectedValue({
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

  it("optimistically updates set-detail cache and rolls back on error", async () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const setDetailKey = ["collection", "set-detail", "SOR", "Star Wars Unlimited"] as const;
    const initialItems: CollectionItem[] = [
      {
        cardPrintingId: 42,
        quantityOwned: 1,
        quantityWanted: 0,
        quantityProxyOwned: 0,
        availability: 1,
        availabilityWithProxies: 1,
        cardId: 10,
        cardName: "Luke Skywalker",
        game: "Star Wars Unlimited",
        set: "SOR",
        number: "001",
        rarity: "Legendary",
        style: "Standard",
        imageUrl: null,
      },
    ];
    queryClient.setQueryData(setDetailKey, initialItems);

    let rejectPost!: (err: unknown) => void;
    vi.spyOn(http, "post").mockImplementation(
      () =>
        new Promise((_resolve, reject) => {
          rejectPost = reject;
        })
    );

    const wrapper = ({ children }: { children: ReactNode }) =>
      createElement(QueryClientProvider, { client: queryClient }, children);

    const { result } = renderHook(() => useSetCardQuantitiesMutation(), { wrapper });

    let promise!: Promise<unknown>;
    await act(async () => {
      promise = result.current
        .mutateAsync({ printingId: 42, quantityOwned: 3, quantityProxyOwned: 0, quantityWanted: 0 })
        .catch((e) => e);
    });

    // Optimistically updated before server responds
    const optimistic = queryClient.getQueryData<CollectionItem[]>(setDetailKey);
    expect(optimistic?.[0].quantityOwned).toBe(3);

    await act(async () => {
      rejectPost(new Error("Network error"));
      await promise;
    });

    // Rolled back on error
    const rolledBack = queryClient.getQueryData<CollectionItem[]>(setDetailKey);
    expect(rolledBack?.[0].quantityOwned).toBe(1);
  });
});
