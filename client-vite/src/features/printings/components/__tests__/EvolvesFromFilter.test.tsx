import { fireEvent, render, screen, waitFor, cleanup } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { EvolvesFromFilter } from "../EvolvesFromFilter";
import * as cardsListApi from "@/features/cards/api/list";

function renderWithQuery(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe("EvolvesFromFilter", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("searches pokemon names and calls onChange when an option is selected", async () => {
    const fetchSpy = vi
      .spyOn(cardsListApi, "fetchPokemonNames")
      .mockResolvedValue(["Pikachu", "Pikachu V", "Pikachu ex"]);
    const onChange = vi.fn();

    renderWithQuery(<EvolvesFromFilter onChange={onChange} />);

    const input = screen.getByRole("combobox", { name: "Evolves From" });
    fireEvent.change(input, { target: { value: "Pika" } });

    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledWith("Pika", 20);
    });

    const option = await screen.findByRole("option", { name: "Pikachu" });
    fireEvent.click(option);

    expect(onChange).toHaveBeenCalledWith("Pikachu");
  });

  it("clears the selected value when clear button is clicked", () => {
    const onChange = vi.fn();
    renderWithQuery(<EvolvesFromFilter value="Pikachu" onChange={onChange} />);

    const input = screen.getByRole("combobox", { name: "Evolves From" }) as HTMLInputElement;
    expect(input.value).toBe("Pikachu");

    fireEvent.click(screen.getByRole("button", { name: "Clear Evolves From filter" }));
    expect(onChange).toHaveBeenCalledWith(undefined);
  });

  it("supports keyboard navigation with ArrowDown, Enter, and Escape", async () => {
    vi.spyOn(cardsListApi, "fetchPokemonNames").mockResolvedValue(["Pikachu", "Pikachu V"]);
    const onChange = vi.fn();

    renderWithQuery(<EvolvesFromFilter onChange={onChange} />);
    const input = screen.getByRole("combobox", { name: "Evolves From" });
    fireEvent.change(input, { target: { value: "Pika" } });

    await screen.findByRole("option", { name: "Pikachu V" });

    fireEvent.keyDown(input, { key: "ArrowDown" });
    fireEvent.keyDown(input, { key: "Enter" });

    expect(onChange).toHaveBeenCalledWith("Pikachu V");

    fireEvent.change(input, { target: { value: "Pika" } });
    await screen.findByRole("option", { name: "Pikachu" });
    fireEvent.keyDown(input, { key: "Escape" });
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });
});
