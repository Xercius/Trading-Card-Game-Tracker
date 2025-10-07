import { useMemo } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import http from "@/lib/http";
import { REQUEST_TIMEOUT_MS } from "@/constants";
import type {
  DryRunParams,
  ImportApplyResponse,
  ImportOptionsResponse,
  ImportPreviewResponse,
} from "./types";

function hashDryRunParams(params: DryRunParams | null): string {
  if (!params) return "none";
  if (params.mode === "remote") {
    return JSON.stringify({ mode: params.mode, source: params.source, set: params.set ?? "" });
  }
  return JSON.stringify({
    mode: params.mode,
    source: params.source,
    name: params.file.name,
    size: params.file.size,
    lastModified: params.file.lastModified,
  });
}

export function useImportOptions() {
  return useQuery<ImportOptionsResponse>({
    queryKey: ["admin", "import", "options"],
    queryFn: async () => {
      const response = await http.get<ImportOptionsResponse>("admin/import/options");
      return response.data;
    },
    staleTime: 5 * 60_000,
  });
}

async function postDryRun(params: DryRunParams) {
  if (params.mode === "remote") {
    const response = await http.post<ImportPreviewResponse>(
      "admin/import/dry-run",
      {
        source: params.source,
        set: params.set,
      },
      { timeout: REQUEST_TIMEOUT_MS }
    );
    return response.data;
  }

  const form = new FormData();
  form.append("source", params.source);
  form.append("file", params.file);
  const response = await http.post<ImportPreviewResponse>("admin/import/dry-run", form, {
    headers: { "Content-Type": "multipart/form-data" },
    timeout: REQUEST_TIMEOUT_MS,
  });
  return response.data;
}

export function useDryRun(params: DryRunParams | null) {
  const key = useMemo(() => ["admin", "import", "dryRun", hashDryRunParams(params)], [params]);
  const query = useQuery<ImportPreviewResponse>({
    queryKey: key,
    enabled: false,
    queryFn: async () => {
      if (!params) throw new Error("Dry-run parameters missing");
      return postDryRun(params);
    },
    retry: false,
  });

  return query;
}

async function postApply(params: DryRunParams) {
  if (params.mode === "remote") {
    const response = await http.post<ImportApplyResponse>(
      "admin/import/apply",
      {
        source: params.source,
        set: params.set,
      },
      { timeout: REQUEST_TIMEOUT_MS }
    );
    return response.data;
  }

  const form = new FormData();
  form.append("source", params.source);
  form.append("file", params.file);
  const response = await http.post<ImportApplyResponse>("admin/import/apply", form, {
    headers: { "Content-Type": "multipart/form-data" },
    timeout: REQUEST_TIMEOUT_MS,
  });
  return response.data;
}

export function useApply() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: postApply,
    onSuccess: (_data, variables) => {
      const key = ["admin", "import", "dryRun", hashDryRunParams(variables)];
      client.removeQueries({ queryKey: key });
    },
  });
}
