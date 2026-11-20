import { useState, type ChangeEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { getErrorMessage } from "@/lib/getErrorMessage";
import {
  applyDecklistImport,
  deckBuilderKeys,
  previewDecklistImport,
  type DecklistImportLineResult,
  type DecklistImportPreviewResponse,
} from "../api";

type Props = {
  deckId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

function readFileAsText(file: File): Promise<string> {
  if (typeof file.text === "function") return file.text();
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? ""));
    reader.onerror = () => reject(reader.error ?? new Error("Failed to read file."));
    reader.readAsText(file);
  });
}

function PreviewLineRow({
  line,
  selectedId,
  onSelectPrinting,
}: {
  line: DecklistImportLineResult;
  selectedId: number | null;
  onSelectPrinting: (printingId: number) => void;
}) {
  const statusBadge =
    line.status === "resolved"
      ? "text-emerald-300"
      : line.status === "ambiguous"
        ? "text-amber-300"
        : "text-red-300";

  return (
    <li className="rounded border border-zinc-700/80 bg-zinc-900/70 p-2 text-xs space-y-1">
      <div className="flex items-center justify-between gap-2">
        <span className="font-medium text-zinc-100">
          {line.quantity > 0 ? `${line.quantity}× ` : ""}
          {line.parsedName}
          <span className="ml-1.5 text-[10px] uppercase text-zinc-400">[{line.zone}]</span>
        </span>
        <span className={`text-[10px] font-semibold uppercase ${statusBadge}`}>{line.status}</span>
      </div>
      {line.message && <p className="text-[11px] text-zinc-400">{line.message}</p>}
      {line.candidates.length > 1 && (
        <select
          aria-label={`Select printing for ${line.parsedName}`}
          value={selectedId ?? ""}
          onChange={(e) => onSelectPrinting(Number(e.target.value))}
          className="w-full rounded border border-zinc-700 bg-zinc-950 px-2 py-1 text-xs text-zinc-100"
        >
          {line.candidates.map((c) => (
            <option key={c.printingId} value={c.printingId}>
              {c.setCode || c.setName || "Unknown Set"} #{c.number} ({c.style})
            </option>
          ))}
        </select>
      )}
    </li>
  );
}

function PreviewSummary({
  preview,
  selections,
  onSelect,
}: {
  preview: DecklistImportPreviewResponse;
  selections: Record<number, number>;
  onSelect: (lineNumber: number, printingId: number) => void;
}) {
  return (
    <div className="space-y-2">
      <div className="text-xs text-zinc-300">
        Resolved: <strong>{preview.resolvedCount}</strong> · Ambiguous:{" "}
        <strong className="text-amber-300">{preview.ambiguousCount}</strong> · Unmatched:{" "}
        <strong className="text-red-300">{preview.unmatchedCount}</strong>
      </div>
      <ul className="max-h-56 space-y-1.5 overflow-y-auto pr-1">
        {preview.lines.map((line) => (
          <PreviewLineRow
            key={line.lineNumber}
            line={line}
            selectedId={selections[line.lineNumber] ?? line.selectedPrintingId}
            onSelectPrinting={(id) => onSelect(line.lineNumber, id)}
          />
        ))}
      </ul>
    </div>
  );
}

/**
 * Dialog to paste or upload a plain-text decklist, preview/resolve printings, and import into a deck.
 */
export function DecklistImportDialog({ deckId, open, onOpenChange }: Props) {
  const queryClient = useQueryClient();
  const [text, setText] = useState("");
  const [replaceExisting, setReplaceExisting] = useState(false);
  const [preview, setPreview] = useState<DecklistImportPreviewResponse | null>(null);
  const [selections, setSelections] = useState<Record<number, number>>({});

  const previewMutation = useMutation({
    mutationFn: (rawText: string) => previewDecklistImport(deckId, rawText),
    onSuccess: (data) => {
      setPreview(data);
      const next: Record<number, number> = {};
      for (const line of data.lines) {
        if (line.selectedPrintingId != null) next[line.lineNumber] = line.selectedPrintingId;
      }
      setSelections(next);
    },
  });

  const applyMutation = useMutation({
    mutationFn: () => {
      const items = (preview?.lines ?? [])
        .map((line) => ({
          cardPrintingId: selections[line.lineNumber] ?? line.selectedPrintingId,
          quantity: line.quantity,
          zone: line.zone,
        }))
        .filter((i): i is { cardPrintingId: number; quantity: number; zone: string } =>
          Boolean(i.cardPrintingId && i.quantity > 0)
        );
      return applyDecklistImport(deckId, { items, replaceExisting });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: deckBuilderKeys.all });
      queryClient.invalidateQueries({ queryKey: ["decks", deckId] });
      onOpenChange(false);
    },
  });

  const handleFileUpload = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;
    const contents = await readFileAsText(file);
    setText(contents);
    setPreview(null);
  };

  const applyCount = (preview?.lines ?? []).filter(
    (l) => (selections[l.lineNumber] ?? l.selectedPrintingId) != null && l.quantity > 0
  ).length;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent labelledBy="decklist-import-title" className="max-w-xl">
        <DialogClose />
        <DialogHeader>
          <DialogTitle id="decklist-import-title">Import Decklist</DialogTitle>
          <DialogDescription>
            Paste a plain-text decklist or upload a .txt file with optional (SET) #collector numbers
            and zone headers (Main:, Sideboard:, Commander:, Leader:, Base:, Starting:, Ideas:).
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-3 p-6">
          <div className="flex items-center justify-between gap-2">
            <input
              type="file"
              accept=".txt,text/plain"
              aria-label="Upload decklist file"
              onChange={handleFileUpload}
              className="text-xs text-zinc-300 file:mr-2 file:rounded file:border-0 file:bg-zinc-700 file:px-2.5 file:py-1 file:text-xs file:text-zinc-100"
            />
            <Button
              type="button"
              size="sm"
              variant="secondary"
              disabled={!text.trim() || previewMutation.isPending}
              onClick={() => previewMutation.mutate(text)}
            >
              {previewMutation.isPending ? "Parsing…" : "Preview"}
            </Button>
          </div>
          <textarea
            aria-label="Decklist text"
            value={text}
            onChange={(e) => {
              setText(e.target.value);
              setPreview(null);
            }}
            rows={7}
            placeholder={"4 Lightning Bolt (M10) 146\nSideboard:\n2 Abrade"}
            className="w-full rounded-md border border-zinc-700 bg-zinc-950 p-2.5 font-mono text-xs text-zinc-100 focus:outline-none focus:ring-1 focus:ring-sky-500"
          />
          <label className="flex items-center gap-2 text-xs text-zinc-300 cursor-pointer">
            <input
              type="checkbox"
              checked={replaceExisting}
              onChange={(e) => setReplaceExisting(e.target.checked)}
            />
            <span>Replace existing cards in deck (unchecked merges with current cards)</span>
          </label>
          {(previewMutation.isError || applyMutation.isError) && (
            <p role="alert" className="text-xs text-red-400">
              {getErrorMessage(
                previewMutation.error || applyMutation.error,
                "Failed to process decklist."
              )}
            </p>
          )}
          {preview && (
            <PreviewSummary
              preview={preview}
              selections={selections}
              onSelect={(lineNum, printingId) =>
                setSelections((prev) => ({ ...prev, [lineNum]: printingId }))
              }
            />
          )}
        </div>
        <DialogFooter>
          <Button type="button" variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            type="button"
            size="sm"
            disabled={applyCount === 0 || applyMutation.isPending}
            onClick={() => applyMutation.mutate()}
          >
            {applyMutation.isPending ? "Importing…" : `Apply Import (${applyCount})`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
