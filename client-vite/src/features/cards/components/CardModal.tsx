import { Fragment, useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { resolveImageUrl } from "@/lib/http";
import { Dialog, DialogContent, DialogClose } from "@/components/ui/dialog";
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

const ASPECT_IMAGE_MAP: Record<string, string> = {
  aggression: "SWH_Aspects_Aggression.png",
  command: "SWH_Aspects_Command.png",
  cunning: "SWH_Aspects_Cunning.png",
  heroism: "SWH_Aspects_Heroism.png",
  vigilance: "SWH_Aspects_Vigilance.png",
  villainy: "SWH_Aspects_Villainy.png",
};

const SET_CODE_TO_LOGO: Record<string, string> = {
  sor: "logo_spark_of_rebellion.png",
  shd: "logo_shadows_of_the_galaxy.png",
  twi: "logo_twilight_of_the_republic.png",
  jtl: "logo_jump_to_lightspeed.png",
  lof: "logo_legends_of_the_force.png",
  sec: "logo_secrets_of_power.png",
  sop: "logo_secrets_of_power.png",
  law: "logo_a_lawless_time.png",
  alt: "logo_a_lawless_time.png",
  ash: "logo_ashes_of_the_empire.png",
  hom: "logo_homeworlds.png",
  hth: "logo_intro_battle_hoth.png",
  icn: "logo_icons_2027.png",
};

function getSetLogoUrl(setCode?: string | null, setName?: string | null): string | null {
  if (setCode) {
    const filename = SET_CODE_TO_LOGO[setCode.toLowerCase().trim()];
    if (filename) {
      return resolveImageUrl(`/images/swu/sets/${filename}`);
    }
  }
  if (setName) {
    const slug = setName.toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "");
    const filename = `logo_${slug}.png`;
    return resolveImageUrl(`/images/swu/sets/${filename}`);
  }
  return null;
}

function SetLogo({
  setCode,
  setName,
}: {
  setCode?: string | null;
  setName?: string | null;
}) {
  const [failed, setFailed] = useState(false);
  const logoUrl = getSetLogoUrl(setCode, setName);

  useEffect(() => {
    setFailed(false);
  }, [logoUrl]);

  if (!logoUrl || failed) {
    return (
      <span
        className="font-semibold text-sm truncate max-w-[170px]"
        title={setName || setCode || undefined}
      >
        {setName || setCode || "—"}
      </span>
    );
  }

  return (
    <img
      src={logoUrl}
      alt={setName || setCode || "Set logo"}
      title={setName || setCode || undefined}
      className="max-h-7 max-w-[150px] object-contain object-left"
      onError={() => setFailed(true)}
    />
  );
}

function StatBadge({
  label,
  value,
  iconPath,
  textColor = "text-white font-black drop-shadow-sm",
}: {
  label: string;
  value: number | string | null | undefined;
  iconPath: string;
  textColor?: string;
}) {
  const displayValue = value != null ? value : "—";
  return (
    <div
      className="relative flex items-center justify-center h-10 w-10 shrink-0"
      aria-label={`${label}: ${displayValue}`}
      title={`${label}: ${displayValue}`}
    >
      <img
        src={resolveImageUrl(iconPath)}
        alt={`${label} badge`}
        className="absolute inset-0 h-full w-full object-contain pointer-events-none select-none drop-shadow"
      />
      <span
        className={`relative z-10 font-black text-lg leading-none select-none ${textColor} ${
          label === "Power" ? "-translate-y-0.5" : ""
        }`}
      >
        <span className="sr-only">{label}: </span>
        {displayValue}
      </span>
    </div>
  );
}

function hasMeaningfulContent(content: string | null | undefined): boolean {
  if (!content || !content.trim()) return false;
  const stripped = content
    .replace(/<[^>]+>/g, "")
    .replace(/&nbsp;/g, " ")
    .trim();
  const hasImages = /<img\b[^>]*>/i.test(content);
  return stripped.length > 0 || hasImages;
}

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

  if (tag === "img") {
    const src = element.getAttribute("src") || "";
    const alt = element.getAttribute("alt") || "";
    return (
      <img
        key={key}
        src={src}
        alt={alt}
        className="inline-block h-4 w-4 align-text-bottom object-contain mx-0.5"
        loading="lazy"
      />
    );
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
    case "skey":
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
        <tbody key={key} className="divide-y border-border/60">
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
 * (`<p>`, `<strong>`, `<i>`, `<em>`, `<img>`, `<br>`, etc.) as React elements without raw `dangerouslySetInnerHTML`.
 */
function renderStyledCardText(
  source: string | null | undefined,
  fallbackMessage?: string
): ReactNode {
  if (!source) {
    return fallbackMessage ? (
      <p className="text-sm italic text-muted-foreground">{fallbackMessage}</p>
    ) : null;
  }

  const trimmed = source.trim();
  if (!trimmed) {
    return fallbackMessage ? (
      <p className="text-sm italic text-muted-foreground">{fallbackMessage}</p>
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

  const frontTypeHeader = useMemo(() => {
    const parts = [primaryType];
    if (isUnitCard && details?.arena) {
      parts.push(details.arena);
    }
    return parts.filter(Boolean).join(" • ");
  }, [primaryType, isUnitCard, details]);

  const backTypeHeader = useMemo(() => {
    return secondaryType || details?.cardType2 || "Back Text";
  }, [secondaryType, details]);

  const rulesTextSource = useMemo(() => {
    if (hasMeaningfulContent(details?.textStyled)) return details!.textStyled!.trim();
    if (hasMeaningfulContent(details?.text)) return details!.text!.trim();
    const desc = (details as { description?: string })?.description;
    if (hasMeaningfulContent(desc)) return desc!.trim();
    return null;
  }, [details]);

  const epicActionSource = useMemo(() => {
    if (hasMeaningfulContent(details?.epicActionStyled)) return details!.epicActionStyled!.trim();
    if (hasMeaningfulContent(details?.epicAction)) return details!.epicAction!.trim();
    return null;
  }, [details]);

  const deployBoxSource = useMemo(() => {
    if (hasMeaningfulContent(details?.deployBoxStyled)) return details!.deployBoxStyled!.trim();
    if (hasMeaningfulContent(details?.deployBox)) return details!.deployBox!.trim();
    return null;
  }, [details]);

  const rulingsSource = useMemo(() => {
    if (hasMeaningfulContent(details?.rulesStyled)) return details!.rulesStyled!.trim();
    if (hasMeaningfulContent(details?.rules)) return details!.rules!.trim();
    return null;
  }, [details]);

  const hasRulings = useMemo(() => {
    if (!rulingsSource) return false;
    return hasMeaningfulContent(rulingsSource);
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
        labelledBy="card-modal-card-name"
        className="relative flex h-[min(calc(100%-20px),62rem)] max-h-[calc(100%-20px)] w-[min(calc(100%-16px),90rem)] max-w-[calc(100%-16px)] flex-col overflow-hidden bg-[#242424] text-card-foreground p-0 gap-0 my-auto mx-2"
      >
        <h2 id="card-modal-card-name" className="sr-only">
          {details ? displayName || details.name : "Card details"}
        </h2>
        {/* Floating Close Button in top right */}
        <DialogClose
          aria-label="Close card dialog"
          className="absolute right-4 top-4 z-30 inline-flex h-8 w-8 items-center justify-center rounded-full text-muted-foreground hover:text-foreground hover:bg-muted/70 transition"
        />

        {/* Main Content Area: Left column takes full height, Right area holds Center/Right columns and bottom Printings */}
        <div className="grid min-h-0 flex-1 grid-cols-1 md:grid-cols-[300px_minmax(0,1fr)] lg:grid-cols-[320px_minmax(0,1fr)] gap-5 p-4 overflow-hidden">
          {/* Left Column: Full Height Card images + Printing metadata */}
          <section
            aria-label="Selected card image"
            className="flex min-h-0 flex-col gap-3 overflow-hidden"
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
              className="rounded-lg border bg-muted/40 p-3 text-xs space-y-2 shrink-0"
              data-testid="selected-printing-metadata"
            >
              {/* Row 1: Rarity and Card Treatment (spaced apart, no subtitles) */}
              <div className="flex items-center justify-between gap-2 text-sm font-semibold text-foreground">
                <span>{selectedPrinting?.rarity || printingDetail?.rarity || "—"}</span>
                <span className="text-right text-muted-foreground font-medium">
                  {activeTreatment}
                </span>
              </div>

              {/* Row 2: Card Set Image with Set Number to the right */}
              <div className="flex items-center justify-between gap-2 pt-1 border-t border-border/50">
                <div className="flex items-center min-w-0 pr-2">
                  <SetLogo
                    setCode={selectedPrinting?.setCode || printingDetail?.set}
                    setName={selectedPrinting?.setName || printingDetail?.set}
                  />
                </div>
                <span className="font-semibold text-sm shrink-0 text-foreground">
                  {selectedPrinting?.number
                    ? `#${selectedPrinting.number}`
                    : printingDetail?.number
                      ? `#${printingDetail.number}`
                      : "—"}
                </span>
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
                    {/* 1. Header (Name with Aspect Logos right next to it, Subtitle underneath, Stats & Traits) */}
                    <div>
                      <div className="flex items-center flex-wrap gap-2.5">
                        <h2
                          data-testid="card-modal-card-name"
                          className="text-2xl font-bold tracking-tight text-foreground"
                        >
                          {displayName || details.name}
                        </h2>

                        {/* Aspect Logos right next to the card title */}
                        {details.aspects && details.aspects.length > 0 && (
                          <div
                            className="flex items-center gap-1.5 shrink-0"
                            aria-label="Card aspects"
                          >
                            {details.aspects.flatMap(({ name, count }) => {
                              const filename = ASPECT_IMAGE_MAP[name.toLowerCase()];
                              const src = filename
                                ? resolveImageUrl(`/images/swu/aspects/${filename}`)
                                : null;
                              return Array.from({ length: count || 1 }, (_, idx) =>
                                src ? (
                                  <img
                                    key={`${name}-${idx}`}
                                    src={src}
                                    alt={name}
                                    title={name}
                                    className="h-7 w-7 object-contain drop-shadow-sm"
                                  />
                                ) : (
                                  <span
                                    key={`${name}-${idx}`}
                                    className="rounded bg-muted px-1.5 py-0.5 text-xs font-semibold"
                                  >
                                    {name}
                                  </span>
                                )
                              );
                            })}
                            <span className="sr-only">
                              {details.aspects
                                .map(({ name, count }) => `${name}${count > 1 ? ` ×${count}` : ""}`)
                                .join(", ")}
                            </span>
                          </div>
                        )}
                      </div>

                      {subtitle && (
                        <p className="text-base font-medium text-muted-foreground mt-0.5">
                          {subtitle}
                        </p>
                      )}

                      {/* 2. Stat Row (Cost, Power, HP with SWU Icon Badges) */}
                      {(details.cost != null || details.power != null || details.hp != null) && (
                        <div
                          aria-label="Card stats"
                          className="flex items-center gap-1.5 mt-2"
                        >
                          <StatBadge
                            label="Cost"
                            value={details.cost}
                            iconPath="/images/swu/icons/cost.png"
                            textColor="text-white font-black drop-shadow-sm"
                          />
                          <StatBadge
                            label="Power"
                            value={details.power}
                            iconPath="/images/swu/icons/power.png"
                            textColor="text-white font-black drop-shadow-sm"
                          />
                          <StatBadge
                            label="HP"
                            value={details.hp}
                            iconPath="/images/swu/icons/hp.png"
                            textColor="text-white font-black drop-shadow-sm"
                          />
                        </div>
                      )}

                      {/* Traits (just beneath cost/power/hp images, no "Traits" subtitle) */}
                      {details.traits && details.traits.length > 0 && (
                        <p className="text-sm italic text-foreground/90 mt-2">
                          {details.traits.join(", ")}
                        </p>
                      )}
                    </div>

                    {/* Front Rules Text (subtitle replaced with front card type + arena) */}
                    <div className="space-y-1">
                      <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                        {frontTypeHeader || "Card Text"}
                      </h3>
                      {renderStyledCardText(rulesTextSource, "No card text available.")}
                    </div>

                    {/* Epic Action (Cards.EpicActionStyled) */}
                    {epicActionSource && (
                      <div className="space-y-1">
                        <h3 className="sr-only">Epic Action</h3>
                        <div className="rounded-md border border-amber-500/70 bg-amber-500/10 p-3 text-sm text-foreground">
                          {renderStyledCardText(epicActionSource)}
                        </div>
                      </div>
                    )}

                    {/* Back Rules Text (subtitle replaced with back card type) */}
                    {deployBoxSource && (
                      <div className="space-y-1">
                        <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                          {backTypeHeader}
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
