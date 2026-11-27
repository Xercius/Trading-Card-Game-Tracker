import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { AdminSwccgAlternatePrintingsSyncResponse, SwccgCsvRowIssue } from "./api";

type Props = {
  result: AdminSwccgAlternatePrintingsSyncResponse | null;
  errorMessage?: string | null;
};

function buildFailureReportText(result: AdminSwccgAlternatePrintingsSyncResponse): string {
  const header = `SWCCG Alternate Printings Failure Report (${result.failedCount} failed of ${result.totalRowsProcessed} rows, dryRun=${result.dryRun})`;
  const rows = result.failures.map(
    (issue) =>
      `[${issue.fileName}:${issue.rowNumber}] ${issue.cardName} (${issue.side} | ${issue.originalSet} -> ${issue.expansion}): ${issue.reason}`
  );
  return [header, ...rows].join("\n");
}

function FailureTableHeader() {
  return (
    <tr className="border-b border-destructive/30 text-left text-xs uppercase text-muted-foreground">
      <th className="px-2 py-1.5 font-medium">File</th>
      <th className="px-2 py-1.5 font-medium">Row #</th>
      <th className="px-2 py-1.5 font-medium">Card Name</th>
      <th className="px-2 py-1.5 font-medium">Side</th>
      <th className="px-2 py-1.5 font-medium">Original Set</th>
      <th className="px-2 py-1.5 font-medium">Expansion</th>
      <th className="px-2 py-1.5 font-medium">Reason</th>
    </tr>
  );
}

function FailureTableRow({ issue }: { issue: SwccgCsvRowIssue }) {
  return (
    <tr className="border-b border-destructive/20 align-top">
      <td className="px-2 py-1.5 font-mono text-xs">{issue.fileName}</td>
      <td className="px-2 py-1.5 font-mono text-xs">{issue.rowNumber}</td>
      <td className="px-2 py-1.5 font-medium">{issue.cardName}</td>
      <td className="px-2 py-1.5">{issue.side}</td>
      <td className="px-2 py-1.5">{issue.originalSet}</td>
      <td className="px-2 py-1.5">{issue.expansion}</td>
      <td className="px-2 py-1.5 text-destructive">{issue.reason}</td>
    </tr>
  );
}

export default function SwccgAlternatePrintingsReport({ result, errorMessage }: Props) {
  const [copied, setCopied] = useState(false);

  if (!result && !errorMessage) {
    return null;
  }

  async function handleCopyReport() {
    if (!result || !navigator.clipboard) {
      return;
    }
    try {
      await navigator.clipboard.writeText(buildFailureReportText(result));
      setCopied(true);
    } catch {
      setCopied(false);
    }
  }

  const hasFailures = Boolean(result && (result.failedCount > 0 || result.failures.length > 0));

  return (
    <div className="space-y-3 border-t border-border pt-3">
      {errorMessage ? (
        <div
          role="alert"
          className="rounded-md border border-destructive bg-destructive/10 p-3 text-sm text-destructive"
        >
          {errorMessage}
        </div>
      ) : null}

      {result ? (
        <section aria-label="SWCCG alternate printings import summary" className="space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex flex-wrap items-center gap-2 text-sm font-medium">
              <span>Alternate Printings Import Summary</span>
              {result.dryRun ? <Badge variant="outline">Dry run</Badge> : null}
              <Badge
                variant={hasFailures ? "outline" : "secondary"}
                className={hasFailures ? "border-destructive text-destructive" : undefined}
              >
                {hasFailures ? `${result.failedCount} failed` : "0 failed"}
              </Badge>
            </div>
            {hasFailures ? (
              <Button type="button" size="sm" variant="outline" onClick={handleCopyReport}>
                {copied ? "Copied Failure Report" : "Copy Failure Report"}
              </Button>
            ) : null}
          </div>

          <dl className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-4">
            <div>
              <dt className="text-muted-foreground">Rows processed</dt>
              <dd className="font-semibold">{result.totalRowsProcessed}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">New cards</dt>
              <dd className="font-semibold">{result.newCardsCreated}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Printings created</dt>
              <dd className="font-semibold">{result.printingsCreated}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Skipped existing</dt>
              <dd className="font-semibold">{result.skippedExisting}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Sets created</dt>
              <dd className="font-semibold">{result.setsCreated}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Rarities created</dt>
              <dd className="font-semibold">{result.raritiesCreated}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Treatments created</dt>
              <dd className="font-semibold">{result.treatmentsCreated}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Failed rows</dt>
              <dd className="font-semibold">{result.failedCount}</dd>
            </div>
          </dl>

          {hasFailures ? (
            <div
              role="alert"
              className="space-y-2 rounded-md border border-destructive bg-destructive/10 p-3"
            >
              <p className="text-sm font-medium text-destructive">
                {result.failedCount} row(s) were not imported successfully. Review the table below
                before re-running.
              </p>
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-sm">
                  <caption className="sr-only">Unimported SWCCG CSV rows</caption>
                  <thead>
                    <FailureTableHeader />
                  </thead>
                  <tbody>
                    {result.failures.map((issue) => (
                      <FailureTableRow
                        key={`${issue.fileName}-${issue.rowNumber}-${issue.cardName}`}
                        issue={issue}
                      />
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : (
            <p role="status" className="text-sm text-muted-foreground">
              All alternate printing CSV rows completed without failures.
            </p>
          )}
        </section>
      ) : null}
    </div>
  );
}
