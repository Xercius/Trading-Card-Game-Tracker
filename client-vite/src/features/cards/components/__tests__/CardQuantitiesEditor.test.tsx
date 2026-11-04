import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import CardQuantitiesEditor from "../CardQuantitiesEditor";
import * as collectionApi from "@/features/collection/api";

describe("CardQuantitiesEditor", () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  function renderWithQuery(ui: React.ReactElement) {
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
  }

  it("renders 3 quantity textboxes with headers Owned, Proxy, Wishlist", () => {
    vi.spyOn(collectionApi, "usePrintingQuantitiesQuery").mockReturnValue({
      data: {
        cardPrintingId: 101,
        quantityOwned: 3,
        quantityProxyOwned: 1,
        quantityWanted: 2,
      },
      isLoading: false,
      isSuccess: true,
    } as unknown as ReturnType<typeof collectionApi.usePrintingQuantitiesQuery>);

    renderWithQuery(<CardQuantitiesEditor printingId={101} />);

    expect(screen.getByText("Owned")).toBeInTheDocument();
    expect(screen.getByText("Proxy")).toBeInTheDocument();
    expect(screen.getByText("Wishlist")).toBeInTheDocument();

    const ownedInput = screen.getByLabelText("Owned") as HTMLInputElement;
    const proxyInput = screen.getByLabelText("Proxy") as HTMLInputElement;
    const wishlistInput = screen.getByLabelText("Wishlist") as HTMLInputElement;

    expect(ownedInput.value).toBe("3");
    expect(proxyInput.value).toBe("1");
    expect(wishlistInput.value).toBe("2");
  });

  it("calls mutation when stepping quantity up or down", () => {
    const mutateMock = vi.fn();
    vi.spyOn(collectionApi, "usePrintingQuantitiesQuery").mockReturnValue({
      data: {
        cardPrintingId: 101,
        quantityOwned: 1,
        quantityProxyOwned: 0,
        quantityWanted: 0,
      },
      isLoading: false,
      isSuccess: true,
    } as unknown as ReturnType<typeof collectionApi.usePrintingQuantitiesQuery>);
    vi.spyOn(collectionApi, "useSetCardQuantitiesMutation").mockReturnValue({
      mutate: mutateMock,
      isPending: false,
    } as unknown as ReturnType<typeof collectionApi.useSetCardQuantitiesMutation>);

    renderWithQuery(<CardQuantitiesEditor printingId={101} />);

    const incOwnedBtn = screen.getByRole("button", { name: "Increase owned" });
    fireEvent.click(incOwnedBtn);

    expect(mutateMock).toHaveBeenCalledWith({
      printingId: 101,
      quantityOwned: 2,
      quantityProxyOwned: 0,
      quantityWanted: 0,
    });
  });

  it("disables quantity controls until the saved quantities load", () => {
    const mutateMock = vi.fn();
    vi.spyOn(collectionApi, "usePrintingQuantitiesQuery").mockReturnValue({
      data: undefined,
      isLoading: true,
      isSuccess: false,
    } as unknown as ReturnType<typeof collectionApi.usePrintingQuantitiesQuery>);
    vi.spyOn(collectionApi, "useSetCardQuantitiesMutation").mockReturnValue({
      mutate: mutateMock,
      isPending: false,
    } as unknown as ReturnType<typeof collectionApi.useSetCardQuantitiesMutation>);

    renderWithQuery(<CardQuantitiesEditor printingId={101} />);

    expect(screen.getByRole("button", { name: "Increase owned" })).toBeDisabled();
    expect(screen.getByLabelText("Owned")).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Increase owned" }));
    expect(mutateMock).not.toHaveBeenCalled();
  });
});
