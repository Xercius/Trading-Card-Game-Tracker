import { useState, type ChangeEvent } from "react";
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
  downloadJsonBackup,
  useImportJsonBackupMutation,
  type ExportPayload,
  type ImportJsonResponse,
  type ImportMode,
} from "../api";

type Props = {
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

function parseBackupJson(rawText: string): ExportPayload {
  const parsed = JSON.parse(rawText) as Partial<ExportPayload>;
  if (!parsed || typeof parsed !== "object" || parsed.version !== 1) {
    throw new Error("Unsupported backup format. Expected a Version 1 JSON backup.");
  }
  return {
    version: 1,
    collection: Array.isArray(parsed.collection) ? parsed.collection : [],
    wishlist: Array.isArray(parsed.wishlist) ? parsed.wishlist : [],
    decks: Array.isArray(parsed.decks) ? parsed.decks : [],
  };
}

function ImportResultSummary({ result }: { result: ImportJsonResponse }) {
  const unresolved = result.unresolved ?? [];
  return (
    <div className="rounded-md border border-emerald-500/40 bg-emerald-950/30 p-3 text-xs space-y-2">
      <div className="font-semibold text-emerald-300">Backup restored successfully</div>
      <div className="text-zinc-200">
        Collection: {result.imported.collection} · Wishlist: {result.imported.wishlist} · Decks:{" "}
        {result.imported.decks} ({result.imported.deckCards} deck cards)
      </div>
      {result.imported.resolvedByNaturalKey > 0 && (
        <div className="text-sky-300">
          Matched {result.imported.resolvedByNaturalKey} printing(s) using card/set identifiers.
        </div>
      )}
      {unresolved.length > 0 && <UnresolvedList unresolved={unresolved} />}
    </div>
  );
}

function UnresolvedList({
  unresolved,
}: {
  unresolved: NonNullable<ImportJsonResponse["unresolved"]>;
}) {
  return (
    <div className="rounded border border-amber-500/40 bg-amber-950/30 p-2 text-amber-200 space-y-1">
      <div className="font-semibold">
        {unresolved.length} item(s) could not be matched in the current catalog:
      </div>
      <ul className="list-disc pl-4 space-y-0.5 max-h-28 overflow-y-auto">
        {unresolved.map((u, idx) => (
          <li key={`${u.section}-${u.cardPrintingId}-${idx}`}>
            [{u.section}] {u.cardName ?? `ID ${u.cardPrintingId}`} — {u.reason}
          </li>
        ))}
      </ul>
    </div>
  );
}

function ExportBackupSection() {
  const [downloading, setDownloading] = useState(false);
  const [downloadMsg, setDownloadMsg] = useState<string | null>(null);

  const handleDownload = async () => {
    setDownloading(true);
    setDownloadMsg(null);
    try {
      const fileName = await downloadJsonBackup();
      setDownloadMsg(`Downloaded ${fileName}`);
    } catch (err) {
      setDownloadMsg(getErrorMessage(err, "Failed to download backup."));
    } finally {
      setDownloading(false);
    }
  };

  return (
    <section className="space-y-2 rounded-lg border border-zinc-700/70 bg-zinc-900/60 p-4">
      <h3 className="text-sm font-semibold text-zinc-100">Export JSON Backup</h3>
      <p className="text-xs text-zinc-400">
        Includes portable card identifiers so backups survive database resets and catalog re-syncs.
      </p>
      <div className="flex items-center gap-3 pt-1">
        <Button type="button" size="sm" onClick={handleDownload} disabled={downloading}>
          {downloading ? "Exporting…" : "Download JSON Backup"}
        </Button>
        {downloadMsg && <span className="text-xs text-zinc-300">{downloadMsg}</span>}
      </div>
    </section>
  );
}

function RestoreModeSelector({
  mode,
  confirmReplace,
  onModeChange,
  onConfirmChange,
}: {
  mode: ImportMode;
  confirmReplace: boolean;
  onModeChange: (next: ImportMode) => void;
  onConfirmChange: (checked: boolean) => void;
}) {
  return (
    <>
      <fieldset className="space-y-1.5">
        <legend className="text-xs font-medium text-zinc-300">Restore Mode</legend>
        <label className="flex items-center gap-2 text-xs text-zinc-200 cursor-pointer">
          <input
            type="radio"
            name="restore-mode"
            value="merge"
            checked={mode === "merge"}
            onChange={() => onModeChange("merge")}
          />
          <span>Merge (keep existing items and add backup entries)</span>
        </label>
        <label className="flex items-center gap-2 text-xs text-amber-200 cursor-pointer">
          <input
            type="radio"
            name="restore-mode"
            value="replace"
            checked={mode === "replace"}
            onChange={() => onModeChange("replace")}
          />
          <span>Replace (overwrite all current collection, wishlist, and deck data)</span>
        </label>
      </fieldset>
      {mode === "replace" && (
        <label className="flex items-center gap-2 rounded border border-red-500/40 bg-red-950/30 p-2 text-xs text-red-200 cursor-pointer">
          <input
            type="checkbox"
            checked={confirmReplace}
            onChange={(e) => onConfirmChange(e.target.checked)}
          />
          <span>
            I understand Replace mode deletes my existing collection, wishlist, and decks.
          </span>
        </label>
      )}
    </>
  );
}

/**
 * Dialog allowing the user to download a portable JSON backup or restore one in Merge or Replace mode.
 */
export function BackupRestoreDialog({ open, onOpenChange }: Props) {
  const [selectedFileName, setSelectedFileName] = useState<string | null>(null);
  const [parsedPayload, setParsedPayload] = useState<ExportPayload | null>(null);
  const [parseError, setParseError] = useState<string | null>(null);
  const [mode, setMode] = useState<ImportMode>("merge");
  const [confirmReplace, setConfirmReplace] = useState(false);
  const [result, setResult] = useState<ImportJsonResponse | null>(null);
  const importMutation = useImportJsonBackupMutation();

  const handleFileChange = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    setParseError(null);
    setResult(null);
    setParsedPayload(null);
    setSelectedFileName(file?.name ?? null);
    if (!file) return;
    try {
      setParsedPayload(parseBackupJson(await readFileAsText(file)));
    } catch (err) {
      setParseError(getErrorMessage(err, "Invalid JSON backup file."));
    }
  };

  const handleRestore = async () => {
    if (!parsedPayload) return;
    setResult(null);
    try {
      setResult(await importMutation.mutateAsync({ payload: parsedPayload, mode }));
    } catch {
      // surfaced via importMutation.error
    }
  };

  const canRestore = Boolean(parsedPayload) && (mode === "merge" || confirmReplace);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent labelledBy="backup-restore-title" className="max-w-lg">
        <DialogClose />
        <DialogHeader>
          <DialogTitle id="backup-restore-title">Backup &amp; Restore</DialogTitle>
          <DialogDescription>
            Export your collection, decks, and wishlist as a portable JSON file, or restore from a
            previous backup.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-5 p-6">
          <ExportBackupSection />
          <section className="space-y-3 rounded-lg border border-zinc-700/70 bg-zinc-900/60 p-4">
            <h3 className="text-sm font-semibold text-zinc-100">Restore from JSON</h3>
            <input
              type="file"
              accept=".json,application/json"
              aria-label="Backup JSON file"
              onChange={handleFileChange}
              className="block w-full text-xs text-zinc-300 file:mr-3 file:rounded file:border-0 file:bg-zinc-700 file:px-3 file:py-1.5 file:text-xs file:font-medium file:text-zinc-100 hover:file:bg-zinc-600"
            />
            {parseError && (
              <p role="alert" className="text-xs text-red-400">
                {parseError}
              </p>
            )}
            {parsedPayload && (
              <div className="rounded border border-zinc-700 bg-zinc-950/60 p-2.5 text-xs text-zinc-300">
                Ready to restore <strong>{selectedFileName}</strong>:{" "}
                {parsedPayload.collection.length} collection, {parsedPayload.wishlist.length}{" "}
                wishlist, {parsedPayload.decks.length} deck(s).
              </div>
            )}
            <RestoreModeSelector
              mode={mode}
              confirmReplace={confirmReplace}
              onModeChange={(next) => {
                setMode(next);
                if (next === "merge") setConfirmReplace(false);
              }}
              onConfirmChange={setConfirmReplace}
            />
            {importMutation.isError && (
              <p role="alert" className="text-xs text-red-400">
                {getErrorMessage(importMutation.error, "Failed to restore backup.")}
              </p>
            )}
            {result && <ImportResultSummary result={result} />}
          </section>
        </div>
        <DialogFooter>
          <Button type="button" variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            Close
          </Button>
          <Button
            type="button"
            size="sm"
            disabled={!canRestore || importMutation.isPending}
            onClick={handleRestore}
          >
            {importMutation.isPending ? "Restoring…" : "Restore Backup"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
