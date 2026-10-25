import {
  AVAILABILITY_DATA,
  AVAILABILITY_PROXY_DATA,
  CARD_IMAGE_DATA,
  CARD_NAME_DATA,
  DRAG_SOURCE_DATA,
  PRINTING_ID_DATA,
} from "./constants";

type DragData = {
  printingId: number | null;
  cardName?: string;
  imageUrl?: string | null;
  availability?: number;
  availabilityWithProxies?: number;
  source?: string;
};

function toIntOrUndef(value: string | null): number | undefined {
  if (!value) return undefined;
  const parsed = Number.parseInt(value.trim(), 10);
  return Number.isFinite(parsed) ? parsed : undefined;
}

export function extractDragData(dataTransfer: DataTransfer): DragData {
  const idValue = dataTransfer.getData(PRINTING_ID_DATA);
  const parsedId = Number.parseInt(idValue, 10);
  const name = dataTransfer.getData(CARD_NAME_DATA) || undefined;
  const image = dataTransfer.getData(CARD_IMAGE_DATA) || undefined;
  const availability = toIntOrUndef(dataTransfer.getData(AVAILABILITY_DATA));
  const availabilityWithProxies = toIntOrUndef(dataTransfer.getData(AVAILABILITY_PROXY_DATA));
  const source = dataTransfer.getData(DRAG_SOURCE_DATA) || undefined;

  return {
    printingId: Number.isFinite(parsedId) ? parsedId : null,
    cardName: name,
    imageUrl: image ?? null,
    availability,
    availabilityWithProxies,
    source,
  } satisfies DragData;
}
