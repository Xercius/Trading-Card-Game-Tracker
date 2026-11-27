import { useState } from "react";
import { useCardPrintings } from "@/features/cards/api";
import { resolveImageUrl } from "@/lib/http";

type PrintingPickerPopoverProps = {
  cardId?: number;
  currentPrintingId: number;
  currentLabel?: string;
  disabled?: boolean;
  actionLabel?: string;
  ariaLabel: string;
  onSelectPrinting: (printingId: number, label: string) => void;
};

/**
 * Opaque popover allowing users to inspect or switch between printings of a card.
 */
export function PrintingPickerPopover({
  cardId,
  currentPrintingId,
  currentLabel,
  disabled = false,
  actionLabel,
  ariaLabel,
  onSelectPrinting,
}: PrintingPickerPopoverProps) {
  const [open, setOpen] = useState(false);
  const { data: printings = [], isLoading } = useCardPrintings(open && cardId ? cardId : 0);

  return (
    <div className="relative inline-block">
      <button
        type="button"
        aria-label={ariaLabel}
        aria-expanded={open}
        disabled={disabled || !cardId}
        onClick={(e) => {
          e.stopPropagation();
          setOpen((prev) => !prev);
        }}
        className="rounded border border-zinc-700/80 bg-zinc-900 px-1.5 py-0.5 text-[11px] font-medium text-zinc-300 hover:bg-zinc-800 hover:text-white disabled:opacity-40"
      >
        {actionLabel ?? currentLabel ?? "Printing"} ▾
      </button>

      {open && (
        <div
          role="listbox"
          aria-label={`${ariaLabel} options`}
          onClick={(e) => e.stopPropagation()}
          className="absolute right-0 z-50 mt-1 max-h-64 w-64 overflow-y-auto rounded-lg border border-zinc-700 bg-zinc-950 p-1.5 shadow-xl"
        >
          <div className="mb-1 flex items-center justify-between px-1.5 text-[11px] font-semibold uppercase tracking-wider text-zinc-400">
            <span>Select Printing</span>
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="rounded px-1 text-zinc-400 hover:bg-zinc-800 hover:text-white"
            >
              ✕
            </button>
          </div>
          {isLoading ? (
            <div className="px-2 py-3 text-xs text-zinc-400">Loading printings…</div>
          ) : printings.length === 0 ? (
            <div className="px-2 py-3 text-xs text-zinc-400">No alternate printings found.</div>
          ) : (
            <ul className="space-y-1">
              {printings.map((p) => {
                const isSelected = p.printingId === currentPrintingId;
                const setLabel = p.setName || p.setCode || "";
                const label = `${setLabel} #${p.number}${p.style ? ` · ${p.style}` : ""}`;
                return (
                  <li key={p.printingId}>
                    <button
                      type="button"
                      role="option"
                      aria-selected={isSelected}
                      onClick={() => {
                        onSelectPrinting(p.printingId, label);
                        setOpen(false);
                      }}
                      className={`flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-xs transition-colors ${
                        isSelected
                          ? "bg-purple-600/30 text-purple-200 ring-1 ring-purple-500/60"
                          : "text-zinc-200 hover:bg-zinc-800"
                      }`}
                    >
                      {p.imageUrl && (
                        <img
                          src={resolveImageUrl(p.imageUrl)}
                          alt=""
                          loading="lazy"
                          decoding="async"
                          className="h-8 w-6 shrink-0 rounded object-cover"
                        />
                      )}
                      <span className="truncate">{label}</span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
