import LazyImage from "@/components/LazyImage";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { resolveImageUrl } from "@/lib/http";
import type { DeckCardWithAvailability } from "./api";
import {
  AVAILABILITY_DATA,
  AVAILABILITY_PROXY_DATA,
  CARD_IMAGE_DATA,
  CARD_NAME_DATA,
  DRAG_SOURCE_DATA,
  DRAG_SOURCE_GRID,
  DRAG_SOURCE_SIDEBAR,
  PRINTING_ID_DATA,
} from "./constants";
import { useMemo } from "react";

type AdjustQuantityMeta = {
  cardName?: string;
  imageUrl?: string | null;
  availability?: number;
  availabilityWithProxies?: number;
};

type Props = {
  deckName: string;
  rows: DeckCardWithAvailability[];
  includeProxies: boolean;
  onIncludeProxiesChange: (next: boolean) => void;
  onAdjustQuantity: (printingId: number, delta: number, meta?: AdjustQuantityMeta) => void;
  isLoading?: boolean;
};

type DragData = {
  printingId: number | null;
  cardName?: string;
  imageUrl?: string | null;
  availability?: number;
  availabilityWithProxies?: number;
  source?: string;
};

function extractDragData(dataTransfer: DataTransfer): DragData {
  const idValue = dataTransfer.getData(PRINTING_ID_DATA);
  const parsedId = Number.parseInt(idValue, 10);
  const name = dataTransfer.getData(CARD_NAME_DATA) || undefined;
  const image = dataTransfer.getData(CARD_IMAGE_DATA) || undefined;
  const availabilityValue = dataTransfer.getData(AVAILABILITY_DATA);
  const availabilityWithProxyValue = dataTransfer.getData(AVAILABILITY_PROXY_DATA);
  const source = dataTransfer.getData(DRAG_SOURCE_DATA) || undefined;

  return {
    printingId: Number.isFinite(parsedId) ? parsedId : null,
    cardName: name,
    imageUrl: image ?? null,
    availability: availabilityValue ? Number.parseInt(availabilityValue, 10) : undefined,
    availabilityWithProxies: availabilityWithProxyValue
      ? Number.parseInt(availabilityWithProxyValue, 10)
      : undefined,
    source,
  } satisfies DragData;
}

export default function StickyDeckSidebar({
  deckName,
  rows,
  includeProxies,
  onIncludeProxiesChange,
  onAdjustQuantity,
  isLoading = false,
}: Props) {
  const total = useMemo(
    () => rows.reduce((sum, row) => sum + row.quantityInDeck, 0),
    [rows]
  );

  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    const data = extractDragData(event.dataTransfer);
    if (data.printingId == null || data.source !== DRAG_SOURCE_GRID) return;

    onAdjustQuantity(data.printingId, 1, {
      cardName: data.cardName,
      imageUrl: data.imageUrl,
      availability: data.availability,
      availabilityWithProxies: data.availabilityWithProxies,
    });
  };

  const handleDragOver = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = "copy";
  };

  const handleDiscardDragOver = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
  };

  const handleDiscardDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    const data = extractDragData(event.dataTransfer);
    if (data.printingId == null || data.source !== DRAG_SOURCE_SIDEBAR) return;

    onAdjustQuantity(data.printingId, -1, {
      cardName: data.cardName,
      imageUrl: data.imageUrl,
      availability: data.availability,
      availabilityWithProxies: data.availabilityWithProxies,
    });
  };

  const handleRowDragStart = (event: React.DragEvent<HTMLLIElement>, row: DeckCardWithAvailability) => {
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData(PRINTING_ID_DATA, String(row.printingId));
    event.dataTransfer.setData(CARD_NAME_DATA, row.cardName);
    if (row.imageUrl) event.dataTransfer.setData(CARD_IMAGE_DATA, row.imageUrl);
    event.dataTransfer.setData(AVAILABILITY_DATA, String(row.availability));
    event.dataTransfer.setData(AVAILABILITY_PROXY_DATA, String(row.availabilityWithProxies));
    event.dataTransfer.setData(DRAG_SOURCE_DATA, DRAG_SOURCE_SIDEBAR);
  };

  return (
    <aside
      className="sticky top-0 flex h-[calc(100vh-64px)] w-80 shrink-0 flex-col border-l bg-background"
      data-testid="deck-sidebar"
    >
      <div className="border-b p-4">
        <h2 className="text-lg font-semibold">{deckName || "Deck"}</h2>
        <div className="mt-1 text-sm text-muted-foreground">Total cards: {total}</div>
        <Button
          size="sm"
          variant={includeProxies ? "default" : "outline"}
          className="mt-3"
          onClick={() => onIncludeProxiesChange(!includeProxies)}
          data-testid="toggle-proxies"
        >
          {includeProxies ? "Showing A + Proxies" : "Showing Owned Only"}
        </Button>
      </div>

      <div
        className="flex-1 overflow-y-auto"
        onDragOver={handleDragOver}
        onDrop={handleDrop}
        data-testid="deck-sidebar-dropzone"
      >
        {isLoading ? (
          <div className="p-4 text-sm text-muted-foreground">Loading deck…</div>
        ) : rows.length === 0 ? (
          <div className="p-4 text-sm text-muted-foreground">Drag cards here to add them to the deck.</div>
        ) : (
          <ul className="divide-y">
            {rows.map((row) => (
              <li
                key={row.printingId}
                className="flex items-start gap-3 p-3"
                draggable
                onDragStart={(event) => handleRowDragStart(event, row)}
                data-testid={`deck-row-${row.printingId}`}
              >
                <div className="h-16 w-12 overflow-hidden rounded border bg-muted">
                  {row.imageUrl ? (
                    <LazyImage
                      src={resolveImageUrl(row.imageUrl)}
                      alt={row.cardName}
                      className="h-full w-full"
                    />
                  ) : (
                    <div className="flex h-full w-full items-center justify-center text-[10px] text-muted-foreground">
                      No image
                    </div>
                  )}
                </div>
                <div className="flex flex-1 flex-col gap-1">
                  <div className="text-sm font-medium leading-tight">{row.cardName}</div>
                  <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                    <span className="font-medium">Qty</span>
                    <Badge variant="secondary" data-testid={`deck-row-${row.printingId}-qty`}>
                      {row.quantityInDeck}
                    </Badge>
                    <Badge variant="outline" data-testid={`deck-row-${row.printingId}-badge-a`}>
                      A {row.availability}
                    </Badge>
                    <Badge
                      variant={includeProxies ? "default" : "outline"}
                      data-testid={`deck-row-${row.printingId}-badge-ap`}
                    >
                      A+P {row.availabilityWithProxies}
                    </Badge>
                  </div>
                </div>
                <Button
                  size="icon"
                  variant="ghost"
                  data-testid={`deck-row-${row.printingId}-decrement`}
                  onClick={() =>
                    onAdjustQuantity(row.printingId, -1, {
                      cardName: row.cardName,
                      imageUrl: row.imageUrl,
                      availability: row.availability,
                      availabilityWithProxies: row.availabilityWithProxies,
                    })
                  }
                  disabled={row.quantityInDeck <= 0}
                  aria-label={`Remove ${row.cardName}`}
                >
                  &minus;
                </Button>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div
        className="border-t bg-muted/40 p-3 text-center text-xs text-muted-foreground"
        onDragOver={handleDiscardDragOver}
        onDrop={handleDiscardDrop}
        data-testid="deck-sidebar-discard"
      >
        Drag a card here to remove one copy
      </div>
    </aside>
  );
}
