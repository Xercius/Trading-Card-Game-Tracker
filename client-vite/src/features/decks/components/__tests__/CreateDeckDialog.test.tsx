import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createRoot } from "react-dom/client";
import { act } from "react";
import { fireEvent, waitFor, screen } from "@testing-library/react";
import { CreateDeckDialog } from "../CreateDeckDialog";
import http from "@/lib/http";

function createClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

describe("CreateDeckDialog", () => {
  let container: HTMLDivElement;
  let root: ReturnType<typeof createRoot>;

  beforeEach(() => {
    container = document.createElement("div");
    document.body.appendChild(container);
    root = createRoot(container);
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    vi.restoreAllMocks();
  });

  it("renders SWU formats and submits new SWU deck", async () => {
    const client = createClient();
    const handleClose = vi.fn();
    const handleDeckCreated = vi.fn();

    const postSpy = vi.spyOn(http, "post").mockResolvedValue({
      data: {
        id: 123,
        userId: 1,
        game: "Star Wars Unlimited",
        name: "Luke Control",
        description: "Late game control",
        format: "Premier",
      },
    } as never);

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <CreateDeckDialog
            isOpen={true}
            onClose={handleClose}
            game="Star Wars Unlimited"
            onDeckCreated={handleDeckCreated}
          />
        </QueryClientProvider>
      );
    });

    expect(document.body.textContent).toContain("Create New Deck");
    expect(document.body.textContent).toContain("Premier");
    expect(document.body.textContent).toContain("Twin Suns");

    const nameInput = document.body.querySelector("input#deck-name-input") as HTMLInputElement;
    const descInput = document.body.querySelector(
      "textarea#deck-desc-input"
    ) as HTMLTextAreaElement;

    await act(async () => {
      fireEvent.change(nameInput, { target: { value: "Luke Control" } });
      fireEvent.change(descInput, { target: { value: "Late game control" } });
    });

    const submitBtn = screen.getByRole("button", { name: "Create & Build Deck" });
    await act(async () => {
      fireEvent.click(submitBtn);
    });

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith("decks", {
        game: "Star Wars Unlimited",
        name: "Luke Control",
        description: "Late game control",
        format: "Premier",
        side: undefined,
      });
      expect(handleDeckCreated).toHaveBeenCalledWith(123);
      expect(handleClose).toHaveBeenCalled();
    });
  });

  it("renders SWCCG side selection and formats, requiring side before submit", async () => {
    const client = createClient();
    const handleClose = vi.fn();
    const handleDeckCreated = vi.fn();

    const postSpy = vi.spyOn(http, "post").mockResolvedValue({
      data: {
        id: 456,
        userId: 1,
        game: "Star Wars CCG",
        name: "Dark Side Space",
        description: "",
        format: "Decipher Only",
        side: "Dark",
      },
    } as never);

    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <CreateDeckDialog
            isOpen={true}
            onClose={handleClose}
            game="Star Wars CCG"
            onDeckCreated={handleDeckCreated}
          />
        </QueryClientProvider>
      );
    });

    expect(document.body.textContent).toContain("Side *");
    expect(document.body.textContent).toContain("Decipher Only");
    expect(document.body.textContent).toContain("Standard");

    const nameInput = document.body.querySelector("input#deck-name-input") as HTMLInputElement;
    await act(async () => {
      fireEvent.change(nameInput, { target: { value: "Dark Side Space" } });
    });

    // Select Dark Side button
    const darkSideBtn = screen.getByRole("button", { name: "Dark Side" });
    await act(async () => {
      fireEvent.click(darkSideBtn);
    });

    // Select Decipher Only format card
    const decipherBtn = screen.getByRole("button", { name: /Decipher Only/i });
    await act(async () => {
      fireEvent.click(decipherBtn);
    });

    const submitBtn = screen.getByRole("button", { name: "Create & Build Deck" });
    await act(async () => {
      fireEvent.click(submitBtn);
    });

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith("decks", {
        game: "Star Wars CCG",
        name: "Dark Side Space",
        description: undefined,
        format: "Decipher Only",
        side: "Dark",
      });
      expect(handleDeckCreated).toHaveBeenCalledWith(456);
      expect(handleClose).toHaveBeenCalled();
    });
  });

  it("shows error if deck name is empty on submission", async () => {
    const client = createClient();
    await act(async () => {
      root.render(
        <QueryClientProvider client={client}>
          <CreateDeckDialog
            isOpen={true}
            onClose={vi.fn()}
            game="Star Wars Unlimited"
            onDeckCreated={vi.fn()}
          />
        </QueryClientProvider>
      );
    });

    const form = document.body.querySelector("form") as HTMLFormElement;
    expect(form).not.toBeNull();

    await act(async () => {
      fireEvent.submit(form);
    });

    expect(document.body.textContent).toContain("Deck name is required.");
  });
});
