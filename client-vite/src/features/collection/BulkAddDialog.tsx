import { useCallback, useEffect, useMemo, useState } from "react";
import type { QueryKey } from "@tanstack/react-query";
import { BULK_DEBOUNCE_MS } from "@/constants";
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useBulkUpdateMutation } from "./api";

type BulkAddDialogProps = {
  queryKey: QueryKey;
};

type ParsedRow = {
  printingId: number;
  ownedDelta: number;
  proxyDelta: number;
};

type ResultRow = ParsedRow & { status: "success" | "error" };

function parseCsvLine(line: string): number[] | null {
  const parts = line.split(",").map((p) => p.trim()).filter(Boolean);
  if (parts.length === 0) return null;
  const numbers = parts.map((p) => Number(p));
  if (numbers.some((n) => !Number.isFinite(n))) return null;
  return numbers;
}

export default function BulkAddDialog({ queryKey }: BulkAddDialogProps) {
  const [open, setOpen] = useState(false);
  const [rawInput, setRawInput] = useState("");
  const [defaultOwnedDelta, setDefaultOwnedDelta] = useState("1");
  const [defaultProxyDelta, setDefaultProxyDelta] = useState("0");
  const [errors, setErrors] = useState<string[]>([]);
  const [results, setResults] = useState<ResultRow[]>([]);

  const mutation = useBulkUpdateMutation(queryKey);

  useEffect(() => {
    if (!open) {
      setErrors([]);
      setResults([]);
      setRawInput("");
    }
  }, [open]);

  useEffect(() => {
    if (results.length === 0) return undefined;
    const timer = window.setTimeout(() => setResults([]), BULK_DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [results]);

  const parseRows = useCallback(() => {
    const lines = rawInput.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
    const parsed: ParsedRow[] = [];
    const nextErrors: string[] = [];
    const ownedFallback = Number(defaultOwnedDelta);
    const proxyFallback = Number(defaultProxyDelta);

    for (const line of lines) {
      const numbers = parseCsvLine(line);
      if (!numbers) {
        nextErrors.push(`Could not parse line: "${line}"`);
        continue;
      }

      if (numbers.length === 1) {
        if (!Number.isFinite(ownedFallback) || !Number.isFinite(proxyFallback)) {
          nextErrors.push(`Line "${line}" missing deltas and defaults are invalid.`);
          continue;
        }
        parsed.push({ printingId: numbers[0], ownedDelta: ownedFallback, proxyDelta: proxyFallback });
      } else if (numbers.length >= 3) {
        parsed.push({ printingId: numbers[0], ownedDelta: numbers[1], proxyDelta: numbers[2] });
      } else {
        nextErrors.push(`Line "${line}" must include printingId[,ownedDelta,proxyDelta].`);
      }
    }

    return { parsed, nextErrors };
  }, [rawInput, defaultOwnedDelta, defaultProxyDelta]);

  const handleSubmit = useCallback(async () => {
    const { parsed, nextErrors } = parseRows();
    setErrors(nextErrors);
    if (parsed.length === 0 || nextErrors.length > 0) return;

    try {
      const payload = parsed.map(({ printingId, ownedDelta, proxyDelta }) => ({ printingId, ownedDelta, proxyDelta }));
      await mutation.mutateAsync({ items: payload });
      setResults(parsed.map((row) => ({ ...row, status: "success" as const })));
      setRawInput("");
    } catch (error) {
      setResults(parsed.map((row) => ({ ...row, status: "error" as const })));
      setErrors(["Bulk update failed. Please try again."]);
    }
  }, [mutation, parseRows]);

  const hasResults = results.length > 0;

  const summary = useMemo(() => {
    if (!hasResults) return null;
    const successCount = results.filter((r) => r.status === "success").length;
    const errorCount = results.length - successCount;
    return { successCount, errorCount };
  }, [hasResults, results]);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <button
        type="button"
        className="rounded border border-input bg-background px-3 py-2 text-sm font-medium transition hover:bg-muted"
        onClick={() => setOpen(true)}
      >
        Bulk add / adjust
      </button>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Bulk update collection</DialogTitle>
          <DialogDescription>
            Paste printing IDs (one per line) or CSV rows in the format <code>printingId,ownedDelta,proxyDelta</code>.
          </DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-4 p-6">
          <label className="flex flex-col gap-2 text-sm">
            <span className="font-medium">Entries</span>
            <textarea
              value={rawInput}
              onChange={(event) => setRawInput(event.target.value)}
              className="min-h-[160px] rounded border border-input bg-background p-3 font-mono text-sm"
              placeholder={"123,2,0\n456"}
            />
          </label>
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="flex flex-col gap-2 text-sm">
              <span className="font-medium">Default owned delta</span>
              <input
                type="number"
                value={defaultOwnedDelta}
                onChange={(event) => setDefaultOwnedDelta(event.target.value)}
                className="rounded border border-input bg-background px-3 py-2"
              />
            </label>
            <label className="flex flex-col gap-2 text-sm">
              <span className="font-medium">Default proxy delta</span>
              <input
                type="number"
                value={defaultProxyDelta}
                onChange={(event) => setDefaultProxyDelta(event.target.value)}
                className="rounded border border-input bg-background px-3 py-2"
              />
            </label>
          </div>
          {errors.length > 0 && (
            <div className="rounded border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive">
              <ul className="list-disc pl-5">
                {errors.map((err) => (
                  <li key={err}>{err}</li>
                ))}
              </ul>
            </div>
          )}
          {hasResults && summary && (
            <div className="rounded border border-muted bg-muted/40 p-3 text-sm">
              <p className="font-medium">Bulk update summary</p>
              <p>{summary.successCount} succeeded</p>
              {summary.errorCount > 0 && <p>{summary.errorCount} failed</p>}
              <ul className="mt-2 max-h-32 overflow-auto font-mono text-xs">
                {results.map((row) => (
                  <li key={`${row.printingId}-${row.ownedDelta}-${row.proxyDelta}-${row.status}`}>
                    #{row.printingId}: ΔOwned {row.ownedDelta}, ΔProxy {row.proxyDelta} – {row.status}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
        <DialogFooter>
          <button
            type="button"
            className="rounded border border-input bg-background px-3 py-2 text-sm transition hover:bg-muted disabled:opacity-60"
            onClick={handleSubmit}
            disabled={mutation.isPending}
          >
            Apply changes
          </button>
          <DialogClose className="rounded border border-input bg-background px-3 py-2 text-sm transition hover:bg-muted">
            Cancel
          </DialogClose>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
