import { type DragEventHandler, useEffect, useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { IMPORT_MAX_ROWS_PREVIEW, UPLOAD_MAX_SIZE_MB } from "@/constants";
import { useApply, useDryRun, useImportOptions } from "@/features/admin/import/hooks";
import type {
  DryRunParams,
  ImportPreviewResponse,
  ImportSourceOption,
} from "@/features/admin/import/types";

const MODE_REMOTE = "remote";
const MODE_UPLOAD = "upload";

function formatBytes(size: number) {
  if (size >= 1_048_576) return `${(size / 1_048_576).toFixed(1)} MB`;
  if (size >= 1024) return `${(size / 1024).toFixed(1)} KB`;
  return `${size} bytes`;
}

export default function AdminImportPage() {
  const [mode, setMode] = useState<typeof MODE_REMOTE | typeof MODE_UPLOAD>(MODE_REMOTE);
  const [sourceKey, setSourceKey] = useState("");
  const [setCode, setSetCode] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [dryRunParams, setDryRunParams] = useState<DryRunParams | null>(null);
  const [preview, setPreview] = useState<ImportPreviewResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ type: "success" | "error"; message: string } | null>(null);

  const { data: options, isLoading, isError } = useImportOptions();
  const sources = options?.sources ?? [];
  const activeSource = useMemo<ImportSourceOption | undefined>(
    () => sources.find((s) => s.key === sourceKey),
    [sources, sourceKey]
  );

  const dryRun = useDryRun(dryRunParams);
  const apply = useApply();

  useEffect(() => {
    if (!dryRunParams) return;
    dryRun.refetch({ throwOnError: false });
  }, [dryRunParams]);

  useEffect(() => {
    if (dryRun.data) {
      setPreview(dryRun.data);
      setError(null);
    }
    if (dryRun.error instanceof Error) {
      setError(dryRun.error.message);
    }
  }, [dryRun.data, dryRun.error]);

  useEffect(() => {
    if (dryRun.isFetching) return;
    if (dryRun.status === "error" && dryRun.error && !(dryRun.error instanceof Error)) {
      setError("Dry run failed.");
    }
  }, [dryRun.isFetching, dryRun.status, dryRun.error]);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 4_000);
    return () => window.clearTimeout(timer);
  }, [toast]);

  useEffect(() => {
    if (mode === MODE_UPLOAD) {
      setSetCode("");
    } else {
      setFile(null);
    }
    setPreview(null);
    setDryRunParams(null);
  }, [mode]);

  const maxFileSize = UPLOAD_MAX_SIZE_MB * 1024 * 1024;

  const resetState = () => {
    setPreview(null);
    setDryRunParams(null);
    setFile(null);
    setSetCode("");
  };

  const buildParams = (): DryRunParams | null => {
    if (!sourceKey) return null;
    if (mode === MODE_REMOTE) {
      return { mode: MODE_REMOTE, source: sourceKey, set: setCode || undefined };
    }
    if (!file) return null;
    return { mode: MODE_UPLOAD, source: sourceKey, file };
  };

  const onDryRun = () => {
    const params = buildParams();
    if (!params) {
      setError("Select a source before running preview.");
      return;
    }
    setError(null);
    setDryRunParams(params);
  };

  const onApply = async () => {
    const params = buildParams();
    if (!params) return;
    try {
      const result = await apply.mutateAsync(params);
      setToast({
        type: "success",
        message: `Import applied — ${result.created} created, ${result.updated} updated`,
      });
      resetState();
    } catch (err) {
      const message = err instanceof Error ? err.message : "Failed to apply import.";
      setToast({ type: "error", message });
    }
  };

  const onFileChosen = (selected: File | null) => {
    if (!selected) {
      setFile(null);
      return;
    }
    const ext = selected.name.split(".").pop()?.toLowerCase();
    if (!ext || !["csv", "json"].includes(ext)) {
      setError("Unsupported file type. Choose a .csv or .json file.");
      setFile(null);
      return;
    }
    if (selected.size > maxFileSize) {
      setError(`File exceeds ${UPLOAD_MAX_SIZE_MB} MB limit.`);
      setFile(null);
      return;
    }
    setError(null);
    setFile(selected);
  };

  const handleDrop: DragEventHandler<HTMLLabelElement> = (event) => {
    event.preventDefault();
    if (event.dataTransfer.files && event.dataTransfer.files[0]) {
      onFileChosen(event.dataTransfer.files[0]);
    }
  };

  const disableDryRun =
    !sourceKey || (mode === MODE_REMOTE ? false : !file) || dryRun.isFetching || apply.isLoading;
  const disableApply = !preview || apply.isLoading || dryRun.isFetching;

  if (isLoading) return <div className="p-4">Loading…</div>;
  if (isError) return <div className="p-4 text-red-500">Failed to load import options.</div>;

  return (
    <div className="p-4 space-y-6">
      <div>
        <h1 className="text-xl font-semibold">Admin · Import</h1>
        <p className="text-sm text-muted-foreground">
          Preview remote imports or upload files before applying changes.
        </p>
      </div>

      {toast && (
        <div
          className={`rounded-md px-3 py-2 text-sm ${
            toast.type === "success"
              ? "bg-emerald-500 text-white"
              : "bg-destructive text-destructive-foreground"
          }`}
        >
          {toast.message}
        </div>
      )}

      <div className="flex gap-2">
        <Button
          type="button"
          variant={mode === MODE_REMOTE ? "default" : "outline"}
          onClick={() => setMode(MODE_REMOTE)}
        >
          Game + Set
        </Button>
        <Button
          type="button"
          variant={mode === MODE_UPLOAD ? "default" : "outline"}
          onClick={() => setMode(MODE_UPLOAD)}
        >
          Upload file
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-3">
          <label className="block text-sm font-medium">Source</label>
          <Select value={sourceKey} onValueChange={(value) => setSourceKey(value)}>
            <SelectTrigger>
              <SelectValue placeholder="Choose an importer" />
            </SelectTrigger>
            <SelectContent>
              {sources.map((src) => (
                <SelectItem key={src.key} value={src.key}>
                  {src.displayName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {activeSource && activeSource.games.length > 0 && (
            <div className="text-xs text-muted-foreground">
              Games: {activeSource.games.join(", ")}
            </div>
          )}
        </div>

        {mode === MODE_REMOTE ? (
          <div className="space-y-3">
            <label className="block text-sm font-medium">Set</label>
            {activeSource && activeSource.sets.length > 0 ? (
              <Select value={setCode} onValueChange={(value) => setSetCode(value)}>
                <SelectTrigger>
                  <SelectValue placeholder="Select set" />
                </SelectTrigger>
                <SelectContent>
                  {activeSource.sets.map((set) => (
                    <SelectItem key={set.code} value={set.code}>
                      {set.name} ({set.code})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            ) : (
              <Input
                value={setCode}
                placeholder="Enter set code (optional)"
                onChange={(event) => setSetCode(event.target.value.toUpperCase())}
              />
            )}
            <p className="text-xs text-muted-foreground">
              Remote imports use the selected set to fetch data from the source API.
            </p>
          </div>
        ) : (
          <div className="space-y-3">
            <label className="block text-sm font-medium">Upload</label>
            <label
              htmlFor="admin-import-file"
              onDragOver={(event) => event.preventDefault()}
              onDrop={handleDrop}
              className="flex min-h-[140px] cursor-pointer flex-col items-center justify-center rounded border-2 border-dashed border-muted-foreground/40 bg-muted/40 px-4 py-6 text-center text-sm"
            >
              <input
                id="admin-import-file"
                type="file"
                accept=".csv,.json"
                className="hidden"
                onChange={(event) => onFileChosen(event.target.files?.[0] ?? null)}
              />
              <span className="font-medium">Drag a .csv or .json file here</span>
              <span className="text-xs text-muted-foreground">or click to browse</span>
              {file && (
                <span className="mt-2 text-xs text-muted-foreground">
                  Selected: {file.name} ({formatBytes(file.size)})
                </span>
              )}
              <span className="mt-2 text-xs text-muted-foreground">
                Up to {UPLOAD_MAX_SIZE_MB} MB • preview limited to {IMPORT_MAX_ROWS_PREVIEW} rows
              </span>
            </label>
          </div>
        )}
      </div>

      {error && (
        <div className="rounded border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      )}

      <div className="flex gap-2">
        <Button type="button" onClick={onDryRun} disabled={disableDryRun}>
          {dryRun.isFetching ? "Running preview…" : "Dry-run"}
        </Button>
        <Button type="button" onClick={onApply} disabled={disableApply} variant="secondary">
          {apply.isLoading ? "Applying…" : "Apply"}
        </Button>
      </div>

      {preview && (
        <div className="space-y-4">
          <div className="flex flex-wrap gap-2">
            <Badge variant="outline">New: {preview.summary.new}</Badge>
            <Badge variant="outline">Update: {preview.summary.update}</Badge>
            <Badge variant="outline">Duplicate: {preview.summary.duplicate}</Badge>
            <Badge variant="outline">Invalid: {preview.summary.invalid}</Badge>
          </div>

          <div className="overflow-x-auto rounded border">
            <table className="min-w-full divide-y divide-border text-sm">
              <thead className="bg-muted/40 text-left text-xs uppercase tracking-wide">
                <tr>
                  <th className="px-3 py-2">Status</th>
                  <th className="px-3 py-2">Name</th>
                  <th className="px-3 py-2">Game</th>
                  <th className="px-3 py-2">Set</th>
                  <th className="px-3 py-2">Messages</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {preview.rows.length === 0 ? (
                  <tr>
                    <td className="px-3 py-4 text-center text-muted-foreground" colSpan={5}>
                      No preview rows were generated by this importer.
                    </td>
                  </tr>
                ) : (
                  preview.rows.map((row) => (
                    <tr key={row.externalId}>
                      <td className="px-3 py-2 align-top">
                        <Badge variant="secondary">{row.status}</Badge>
                      </td>
                      <td className="px-3 py-2 align-top">{row.name}</td>
                      <td className="px-3 py-2 align-top">{row.game}</td>
                      <td className="px-3 py-2 align-top">{row.set}</td>
                      <td className="px-3 py-2 align-top">
                        <ul className="list-disc space-y-1 pl-4">
                          {row.messages.map((message, index) => (
                            <li key={index}>{message}</li>
                          ))}
                        </ul>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
