import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import CardTile from "../CardTile";

describe("CardTile", () => {
  it("lets card images fill the available width at their natural height", () => {
    render(
      <CardTile
        card={{
          id: 1,
          primaryPrintingId: 1,
          name: "Landscape Card",
          game: "Test Game",
          imageUrl: "https://example.com/card.jpg",
        }}
      />
    );

    const image = screen.getByRole("button").querySelector("img");
    expect(image?.classList.contains("w-full")).toBe(true);
    expect(image?.classList.contains("h-auto")).toBe(true);
    expect(image?.parentElement?.classList.contains("aspect-[63/88]")).toBe(false);
  });
});
