import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
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
import { deckBuilderKeys, fetchDecklistExportText } from "../api";

type Props = {
  deckId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

/**
 * Dialog displaying a deck's formatted plain-text list with copy-to-clipboard and .txt download actions.
 */
export function DecklistExportDialog({ deckId, open, onOpenChange }: Props) {
  const [copied, setCopied] = useState(false);

  const exportQuery = useQuery({
    queryKey: deckBuilderKeys.exportText(deckId),
    queryFn: () => fetchDecklistExportText(deckId),
    enabled: open && deckId > 0,
    staleTime: 0,
  });

  const text = exportQuery.data?.text ?? "";
  const fileName = exportQuery.data?.fileName ?? "deck.txt";

  const handleCopy = async () => {
    if (!text) return;
    await navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleDownload = () => {
    if (!text) return;
    const blob = new Blob([text], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    try {
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
    } finally {
      URL.revokeObjectURL(url);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent labelledBy="decklist-export-title" className="max-w-lg">
        <DialogClose />
        <DialogHeader>
          <DialogTitle id="decklist-export-title">Export Decklist</DialogTitle>
          <DialogDescription>
            Copy this formatted decklist to the clipboard or download it as a .txt file.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-3 p-6">
          {exportQuery.isLoading && <p className="text-xs text-zinc-400">Formatting decklist…</p>}
          {exportQuery.isError && (
            <p role="alert" className="text-xs text-red-400">
              {getErrorMessage(exportQuery.error, "Failed to export decklist.")}
            </p>
          )}
          <textarea
            readOnly
            aria-label="Exported decklist text"
            value={text}
            rows={12}
            className="w-full rounded-md border border-zinc-700 bg-zinc-950 p-2.5 font-mono text-xs text-zinc-100"
          />
        </div>
        <DialogFooter>
          <Button type="button" variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            Close
          </Button>
          <Button type="button" variant="secondary" size="sm" disabled={!text} onClick={handleCopy}>
            {copied ? "Copied!" : "Copy to Clipboard"}
          </Button>
          <Button type="button" size="sm" disabled={!text} onClick={handleDownload}>
            Download .txt
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
