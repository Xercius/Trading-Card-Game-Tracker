import { Fragment, useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { resolveImageUrl } from "@/lib/http";
import {
  Dialog,
  DialogContent,
  DialogClose,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useCardDetails, useCardPrintings, type PrintingSummary } from "../api";
import CardImages from "./CardImages";

type CardModalProps = {
  cardId: number;
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  initialPrintingId?: number;
};

const EMPTY_PRINTINGS: PrintingSummary[] = [];
const BLOCKED_TAGS = new Set(["script", "style", "iframe", "object", "embed"]);

function domNodeToReact(node: ChildNode, key: string): ReactNode {
  if (node.nodeType === Node.TEXT_NODE) {
    return node.textContent;
  }
  if (node.nodeType !== Node.ELEMENT_NODE) {
    return null;
  }

  const element = node as Element;
  const tag = element.tagName.toLowerCase();
  if (BLOCKED_TAGS.has(tag)) {
    return null;
  }

  if (tag === "br") {
    return <br key={key} />;
  }

  const children = Array.from(element.childNodes).map((child, idx) =>
    domNodeToReact(child, `${key}-${idx}`)
  );

  switch (tag) {
    case "p":
      return (
        <p key={key} className="whitespace-pre-line text-sm leading-relaxed">
          {children}
        </p>
      );
    case "strong":
    case "b":
      return <strong key={key}>{children}</strong>;
    case "i":
    case "em":
      return <i key={key}>{children}</i>;
    case "u":
      return <u key={key}>{children}</u>;
    case "sub":
      return <sub key={key}>{children}</sub>;
    case "sup":
      return <sup key={key}>{children}</sup>;
    case "ul":
      return (
        <ul key={key} className="list-disc pl-5 text-sm leading-relaxed">
          {children}
        </ul>
      );
    case "ol":
      return (
        <ol key={key} className="list-decimal pl-5 text-sm leading-relaxed">
          {children}
        </ol>
      );
    case "li":
      return <li key={key}>{children}</li>;
    case "span":
      return <span key={key}>{children}</span>;
    case "figure":
      return (
        <figure key={key} className="my-1 w-full overflow-x-auto">
          {children}
        </figure>
      );
    case "table":
      return (
        <table key={key} className="w-full border-collapse border border-border/60 text-xs">
          {children}
        </table>
      );
    case "thead":
      return (
        <thead key={key} className="border-b border-border/60 bg-muted/40">
          {children}
        </thead>
      );
    case "tbody":
      return (
        <tbody key={key} className="divide-y divide-border/60">
          {children}
        </tbody>
      );
    case "tr":
      return (
        <tr
          key={key}
          className="border-b border-border/60 last:border-b-0 hover:bg-muted/10 transition-colors"
        >
          {children}
        </tr>
      );
    case "th":
      return (
        <th
          key={key}
          className="border border-border/60 p-2.5 text-left font-semibold text-xs text-muted-foreground align-top"
        >
          {children}
        </th>
      );
    case "td":
      return (
        <td
          key={key}
          className="border border-border/60 p-2.5 text-xs align-top leading-relaxed first:whitespace-nowrap first:font-medium first:text-muted-foreground"
        >
          {children}
        </td>
      );
    default:
      return <Fragment key={key}>{children}</Fragment>;
  }
}

/**
 * Safely renders styled card text containing basic HTML formatting tags
 * (`<p>`, `<strong>`, `<i>`, `<em>`, `<br>`, etc.) as React elements without raw `dangerouslySetInnerHTML`.
 */
function renderStyledCardText(
  content: string | null | undefined,
  fallbackText?: string
): ReactNode {
  const trimmed = content?.trim();
  if (!trimmed) {
    return fallbackText ? (
      <p className="whitespace-pre-line text-sm leading-relaxed text-muted-foreground">
        {fallbackText}
      </p>
    ) : null;
  }

  if (!/<[a-z][\s\S]*>/i.test(trimmed)) {
    return <p className="whitespace-pre-line text-sm leading-relaxed">{trimmed}</p>;
  }

  const doc = new DOMParser().parseFromString(trimmed, "text/html");
  const nodes = Array.from(doc.body.childNodes).map((node, index) =>
    domNodeToReact(node, `styled-${index}`)
  );
  const hasBlockElement = Array.from(doc.body.children).some((el) => {
    const t = el.tagName.toLowerCase();
    return ["p", "div", "figure", "table", "ul", "ol", "blockquote"].includes(t);
  });

  if (!hasBlockElement) {
    return <p className="whitespace-pre-line text-sm leading-relaxed">{nodes}</p>;
  }

  return <div className="space-y-2">{nodes}</div>;
}

export default function CardModal({
  cardId,
  open,
  onOpenChange,
  initialPrintingId,
}: CardModalProps) {
  const detailsQuery = useCardDetails(open ? cardId : 0);
  const printingsQuery = useCardPrintings(open ? cardId : 0);

  const [selectedPrintingId, setSelectedPrintingId] = useState<number | null>(
    initialPrintingId ?? null
  );

  const printings = printingsQuery.data ?? EMPTY_PRINTINGS;
  const details = detailsQuery.data;

  const { displayName, subtitle } = useMemo(() => {
    if (!details) return { displayName: "", subtitle: null };
    const rawSubtitle = details.subtitle?.trim() || null;
    let name = details.name?.trim() || "";
    if (rawSubtitle) {
      const escaped = rawSubtitle.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
      const pattern = new RegExp(`\\s*[\u2014\\-]\\s*${escaped}$`, "i");
      name = name.replace(pattern, "").trim();
    }
    return { displayName: name, subtitle: rawSubtitle };
  }, [details]);

  const primaryType = useMemo(() => {
    if (!details) return "";
    return details.cardType?.trim() || details.cardTypes?.[0]?.trim() || "";
  }, [details]);

  const secondaryType = useMemo(() => {
    if (!details) return null;
    const explicitType2 = details.cardType2?.trim();
    if (explicitType2 && explicitType2.toLowerCase() !== primaryType.toLowerCase()) {
      return explicitType2;
    }
    const extraType = details.cardTypes?.find(
      (t) => t.trim().length > 0 && t.trim().toLowerCase() !== primaryType.toLowerCase()
    );
    return extraType?.trim() ?? null;
  }, [details, primaryType]);

  const isUnitCard = useMemo(() => {
    if (!details) return false;
    const allTypes = [primaryType, secondaryType ?? "", ...(details.cardTypes ?? [])];
    return allTypes.some((type) => type.toLowerCase().includes("unit"));
  }, [details, primaryType, secondaryType]);

  const rulesTextSource =
    details?.textStyled?.trim() ||
    details?.text?.trim() ||
    (details as { description?: string })?.description?.trim() ||
    null;
  const epicActionSource = details?.epicActionStyled?.trim() || details?.epicAction?.trim() || null;
  const deployBoxSource = details?.deployBoxStyled?.trim() || details?.deployBox?.trim() || null;
  const rulingsSource = details?.rulesStyled?.trim() || details?.rules?.trim() || null;

  const hasRulings = useMemo(() => {
    if (!rulingsSource) return false;
    const stripped = rulingsSource.replace(/<[^>]*>/g, "").trim();
    return stripped.length > 0;
  }, [rulingsSource]);

  useEffect(() => {
    if (!open) return;
    setSelectedPrintingId(initialPrintingId ?? null);
  }, [cardId, initialPrintingId, open]);

  useEffect(() => {
    if (!open) return;
    if (selectedPrintingId != null) return;
    const fallback = printings[0]?.printingId ?? details?.printings[0]?.id ?? null;
    if (fallback != null) setSelectedPrintingId(fallback);
  }, [details, open, printings, selectedPrintingId]);

  const selectedPrinting: PrintingSummary | null = useMemo(() => {
    if (selectedPrintingId == null) return null;
    return printings.find((p) => p.printingId === selectedPrintingId) ?? null;
  }, [printings, selectedPrintingId]);

  const printingDetail = useMemo(() => {
    if (selectedPrintingId == null || !details?.printings) return null;
    return details.printings.find((p) => p.id === selectedPrintingId) ?? null;
  }, [details, selectedPrintingId]);

  const activeTreatment = useMemo(() => {
    if (printingDetail?.style && printingDetail.style !== "Standard") {
      return printingDetail.style;
    }
    if (selectedPrinting?.style && selectedPrinting.style !== "Standard") {
      return selectedPrinting.style;
    }
    return printingDetail?.style || selectedPrinting?.style || "Standard";
  }, [printingDetail, selectedPrinting]);

  const enrichedPrintings = useMemo(() => {
    return printings.map((p) => {
      const match = details?.printings?.find((d) => d.id === p.printingId);
      const style =
        match?.style && match.style !== "Standard"
          ? match.style
          : p.style || match?.style || "Standard";
      return { ...p, style };
    });
  }, [printings, details]);

  const handleCarouselKeyDown = useCallback(
    (event: React.KeyboardEvent<HTMLDivElement>) => {
      if (printings.length === 0) return;
      if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
      event.preventDefault();
      const currentIndex = printings.findIndex((p) => p.printingId === selectedPrintingId);
      if (currentIndex === -1) {
        setSelectedPrintingId(printings[0].printingId);
        return;
      }
      if (event.key === "ArrowRight") {
        const next = printings[(currentIndex + 1) % printings.length];
        setSelectedPrintingId(next.printingId);
      } else {
        const prev = printings[(currentIndex - 1 + printings.length) % printings.length];
        setSelectedPrintingId(prev.printingId);
      }
    },
    [printings, selectedPrintingId]
  );

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        labelledBy="card-modal-title"
        className="flex h-[min(calc(100%-20px),62rem)] max-h-[calc(100%-20px)] w-[min(calc(100%-16px),90rem)] max-w-[calc(100%-16px)] flex-col overflow-hidden bg-[#242424] text-card-foreground p-0 gap-0 my-auto mx-2"
      >
        <DialogHeader className="px-6 py-4 border-b shrink-0 flex flex-row items-center justify-between">
          <DialogTitle id="card-modal-title" className="text-xl font-semibold">
            {details?.name ?? "Loading card..."}
          </DialogTitle>
          <DialogClose aria-label="Close card dialog" />
        </DialogHeader>

        {/* Main Content Area: Left column takes full height, Right area holds Center/Right columns and bottom Printings */}
        <div className="grid min-h-0 flex-1 grid-cols-1 md:grid-cols-[300px_minmax(0,1fr)] lg:grid-cols-[320px_minmax(0,1fr)] gap-5 p-4 overflow-hidden">
          {/* Left Column: Full Height Card images + Printing metadata */}
          <section
            aria-label="Selected card image"
            className="flex min-h-0 flex-col gap-4 overflow-hidden"
          >
            <div className="min-h-0 flex-1 w-full">
              <CardImages
                name={details?.name ?? "Selected printing"}
                imageUrl={selectedPrinting?.imageUrl ?? printingDetail?.imageUrl ?? null}
                backImageUrl={selectedPrinting?.backImageUrl ?? printingDetail?.backImageUrl}
                imageOrientation={
                  selectedPrinting?.imageOrientation ?? printingDetail?.imageOrientation
                }
                backImageOrientation={
                  selectedPrinting?.backImageOrientation ?? printingDetail?.backImageOrientation
                }
              />
            </div>

            {/* Printing metadata below card image */}
            <div
              className="rounded-lg border bg-card/60 p-3 text-xs space-y-2 shrink-0"
              data-testid="selected-printing-metadata"
            >
              <div className="flex justify-between items-baseline gap-2">
                <span className="font-medium text-muted-foreground">Rarity</span>
                <span className="font-semibold text-right">
                  {selectedPrinting?.rarity || printingDetail?.rarity || "—"}
                </span>
              </div>
              <div className="flex justify-between items-baseline gap-2">
                <span className="font-medium text-muted-foreground">Set Name</span>
                <span className="font-semibold text-right">
                  {selectedPrinting?.setName || printingDetail?.set || "—"}
                </span>
              </div>
              <div className="flex justify-between items-baseline gap-2">
                <span className="font-medium text-muted-foreground">Set Number</span>
                <span className="font-semibold text-right">
                  {selectedPrinting?.number
                    ? `#${selectedPrinting.number}`
                    : printingDetail?.number
                      ? `#${printingDetail.number}`
                      : "—"}
                </span>
              </div>
              <div className="flex justify-between items-baseline gap-2">
                <span className="font-medium text-muted-foreground">Card Treatment</span>
                <span className="font-semibold text-right">{activeTreatment}</span>
              </div>
            </div>

            {/* Accessible label for printing details */}
            <div className="sr-only" data-testid="selected-printing-label">
              {selectedPrinting ? (
                <>
                  <span className="font-medium">{selectedPrinting.setName}</span>
                  {selectedPrinting.setCode ? ` (${selectedPrinting.setCode})` : ""}
                  {selectedPrinting.number ? ` • #${selectedPrinting.number}` : ""}
                  {selectedPrinting.rarity ? ` • ${selectedPrinting.rarity}` : ""}
                  {selectedPrinting.releaseDate ? ` • ${selectedPrinting.releaseDate}` : ""}
                  {activeTreatment ? ` • ${activeTreatment}` : ""}
                </>
              ) : (
                "Select a printing"
              )}
            </div>
          </section>

          {/* Right Area: Center/Right Columns (top) + Printings Carousel (bottom) */}
          <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-hidden">
            {/* Top: Center Column (details) + Right Column (rulings, if present) */}
            <div
              className={`grid min-h-0 flex-1 gap-5 overflow-y-auto pr-1 ${
                hasRulings
                  ? "grid-cols-1 md:grid-cols-[minmax(0,1.15fr)_minmax(280px,1fr)]"
                  : "grid-cols-1"
              }`}
            >
              {/* Center Column: Card Details */}
              <section
                aria-label="Card details and actions"
                className="flex min-h-0 flex-col gap-5 overflow-y-auto pr-1"
              >
                {detailsQuery.isLoading && <p className="text-sm">Loading details…</p>}
                {detailsQuery.isError && (
                  <p className="text-sm text-destructive">Failed to load card details.</p>
                )}

                {details && (
                  <div className="space-y-4 text-sm" aria-live="polite">
                    {/* 1. Card Name & Subtitle */}
                    <div>
                      <h2
                        data-testid="card-modal-card-name"
                        className="text-2xl font-bold tracking-tight text-foreground"
                      >
                        {displayName || details.name}
                      </h2>
                      {subtitle && (
                        <p className="text-lg font-medium text-muted-foreground mt-0.5">
                          {subtitle}
                        </p>
                      )}
                    </div>

                    {/* 2. Stat Row (Cost, Power, HP) */}
                    {(details.cost != null || details.power != null || details.hp != null) && (
                      <div
                        aria-label="Card stats"
                        className="flex items-center gap-6 rounded-lg border bg-muted/40 px-4 py-2 w-fit"
                      >
                        <div className="flex flex-col items-center min-w-[2.5rem]">
                          <span className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
                            Cost
                          </span>
                          <span className="text-lg font-bold">
                            {details.cost != null ? details.cost : "—"}
                          </span>
                        </div>
                        <div className="h-7 w-px bg-border" />
                        <div className="flex flex-col items-center min-w-[2.5rem]">
                          <span className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
                            Power
                          </span>
                          <span className="text-lg font-bold">
                            {details.power != null ? details.power : "—"}
                          </span>
                        </div>
                        <div className="h-7 w-px bg-border" />
                        <div className="flex flex-col items-center min-w-[2.5rem]">
                          <span className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
                            HP
                          </span>
                          <span className="text-lg font-bold">
                            {details.hp != null ? details.hp : "—"}
                          </span>
                        </div>
                      </div>
                    )}

                    {/* 3. Listed top to bottom in exact order: */}

                    {/* A. Type(s) */}
                    <div className="space-y-1">
                      <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                        Type
                      </h3>
                      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm font-medium">
                        <span>{primaryType}</span>
                        {secondaryType && (
                          <>
                            <span className="text-muted-foreground">•</span>
                            <span>{secondaryType}</span>
                          </>
                        )}
                        {isUnitCard && details.arena && (
                          <>
                            <span className="text-muted-foreground">•</span>
                            <span>{details.arena}</span>
                          </>
                        )}
                        {details.aspects && details.aspects.length > 0 && (
                          <>
                            <span className="text-muted-foreground">•</span>
                            <span>
                              {details.aspects
                                .map(({ name, count }) => `${name}${count > 1 ? ` ×${count}` : ""}`)
                                .join(", ")}
                            </span>
                          </>
                        )}
                      </div>
                    </div>

                    {/* B. Traits */}
                    {details.traits && details.traits.length > 0 && (
                      <div className="space-y-1">
                        <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                          Traits
                        </h3>
                        <p className="text-sm italic text-foreground/90">
                          {details.traits.join(", ")}
                        </p>
                      </div>
                    )}

                    {/* C. Card text (Cards.TextStyled) */}
                    <div className="space-y-1">
                      <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                        Card Text
                      </h3>
                      {renderStyledCardText(rulesTextSource, "No card text available.")}
                    </div>

                    {/* D. Epic Action (Cards.EpicActionStyled) */}
                    {epicActionSource && (
                      <div className="space-y-1">
                        <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                          Epic Action
                        </h3>
                        {renderStyledCardText(epicActionSource)}
                      </div>
                    )}

                    {/* E. Back text (Cards.DeployBoxStyled) */}
                    {deployBoxSource && (
                      <div className="space-y-1">
                        <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                          Back Text
                        </h3>
                        {renderStyledCardText(deployBoxSource)}
                      </div>
                    )}
                  </div>
                )}
              </section>

              {/* Right Column: Card Rulings (Conditional) */}
              {hasRulings && (
                <section
                  aria-label="Card rulings"
                  className="flex min-h-0 flex-col gap-3 rounded-lg border bg-card/40 p-4 overflow-y-auto"
                >
                  <h2 className="text-base font-semibold tracking-tight">Rulings</h2>
                  <div className="space-y-2 text-sm text-foreground/90">
                    {renderStyledCardText(rulingsSource)}
                  </div>
                </section>
              )}
            </div>

            {/* Bottom Section: Printings carousel placed ONLY under center and right columns */}
            <section
              aria-label="Card printings"
              className="shrink-0 border-t bg-card/20 pt-3 pb-1 space-y-2"
            >
              <h2 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Printings
              </h2>
              <div className="space-y-2" aria-live="polite">
                {printingsQuery.isLoading && (
                  <p className="text-xs text-muted-foreground">Loading printings…</p>
                )}
                {printingsQuery.isError && (
                  <p className="text-xs text-destructive">Failed to load printings.</p>
                )}
                {printings.length > 0 ? (
                  <div
                    role="listbox"
                    tabIndex={0}
                    onKeyDown={handleCarouselKeyDown}
                    className="flex gap-3 overflow-x-auto pb-1 focus:outline-none"
                    aria-label="Card printing options"
                  >
                    {enrichedPrintings.map((printing) => {
                      const isActive = printing.printingId === selectedPrintingId;
                      const setCode = printing.setCode || printing.setName;
                      const numberText = printing.number ? `#${printing.number}` : "";
                      const treatmentText =
                        printing.style && printing.style !== "Standard"
                          ? ` (${printing.style})`
                          : "";
                      return (
                        <button
                          key={printing.printingId}
                          type="button"
                          role="option"
                          aria-selected={isActive}
                          aria-label={`${printing.setName} ${numberText}${treatmentText}`.trim()}
                          title={`${printing.setName} ${numberText}${treatmentText}`}
                          className={`flex w-20 shrink-0 flex-col items-center gap-1.5 rounded-lg border p-1.5 text-xs transition focus:outline-none focus:ring-2 focus:ring-primary ${
                            isActive
                              ? "border-primary bg-primary/10 ring-1 ring-primary"
                              : "border-border hover:border-primary/50"
                          }`}
                          onClick={() => setSelectedPrintingId(printing.printingId)}
                        >
                          <div className="h-20 w-full rounded bg-muted overflow-hidden">
                            {printing.imageUrl ? (
                              <img
                                src={resolveImageUrl(printing.imageUrl)}
                                alt={printing.setName}
                                loading="lazy"
                                className="h-full w-full object-contain"
                              />
                            ) : (
                              <span className="flex h-full w-full items-center justify-center text-[9px] text-muted-foreground">
                                No image
                              </span>
                            )}
                          </div>
                          <span className="text-[11px] text-center font-medium line-clamp-1">
                            {setCode} {numberText}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                ) : (
                  !printingsQuery.isLoading && (
                    <p className="text-xs text-muted-foreground">No alternate printings found.</p>
                  )
                )}
              </div>
            </section>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
