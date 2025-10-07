import { describe, expect, it } from "vitest";

import { extractDragData } from "../StickyDeckSidebar";
import {
  AVAILABILITY_DATA,
  AVAILABILITY_PROXY_DATA,
  CARD_IMAGE_DATA,
  CARD_NAME_DATA,
  DRAG_SOURCE_DATA,
  PRINTING_ID_DATA,
} from "../constants";

function createDataTransfer(values: Record<string, string | undefined>): DataTransfer {
  return {
    getData(type: string) {
      return values[type] ?? "";
    },
  } as unknown as DataTransfer;
}

describe("extractDragData", () => {
  it("parses valid numbers", () => {
    const dataTransfer = createDataTransfer({
      [PRINTING_ID_DATA]: "42",
      [CARD_NAME_DATA]: "Sample Card",
      [CARD_IMAGE_DATA]: "image.jpg",
      [AVAILABILITY_DATA]: "5",
      [AVAILABILITY_PROXY_DATA]: "7",
      [DRAG_SOURCE_DATA]: "grid",
    });

    const result = extractDragData(dataTransfer);

    expect(result).toEqual({
      printingId: 42,
      cardName: "Sample Card",
      imageUrl: "image.jpg",
      availability: 5,
      availabilityWithProxies: 7,
      source: "grid",
    });
  });

  it("returns undefined availability when the value is not numeric", () => {
    const dataTransfer = createDataTransfer({
      [PRINTING_ID_DATA]: "99",
      [AVAILABILITY_DATA]: "not a number",
      [AVAILABILITY_PROXY_DATA]: "12",
    });

    const result = extractDragData(dataTransfer);

    expect(result).toEqual({
      printingId: 99,
      cardName: undefined,
      imageUrl: null,
      availability: undefined,
      availabilityWithProxies: 12,
      source: undefined,
    });
  });

  it("returns null printingId when the id is not numeric", () => {
    const dataTransfer = createDataTransfer({
      [PRINTING_ID_DATA]: "abc",
      [AVAILABILITY_DATA]: "3",
    });

    const result = extractDragData(dataTransfer);

    expect(result).toEqual({
      printingId: null,
      cardName: undefined,
      imageUrl: null,
      availability: 3,
      availabilityWithProxies: undefined,
      source: undefined,
    });
  });
});
