import { useCallback, useEffect, useState } from "react";
import type { QueryKey } from "@tanstack/react-query";
import { MAX_QTY, MIN_QTY } from "@/constants";
import { useSetOwnedProxyMutation } from "./api";

type QtyFieldProps = {
  printingId: number;
  value: number;
  otherValue: number;
  field: "owned" | "proxy";
  queryKey: QueryKey;
};

function clamp(value: number) {
  if (!Number.isFinite(value)) return MIN_QTY;
  const intValue = Math.trunc(value);
  if (intValue < MIN_QTY) return MIN_QTY;
  if (intValue > MAX_QTY) return MAX_QTY;
  return intValue;
}

export default function QtyField({
  printingId,
  value,
  otherValue,
  field,
  queryKey,
}: QtyFieldProps) {
  const mutation = useSetOwnedProxyMutation(queryKey);
  const [draft, setDraft] = useState(() => String(value));
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setDraft(String(value));
  }, [value]);

  const commit = useCallback(
    async (raw: string) => {
      const next = clamp(Number(raw));
      const current = field === "owned" ? value : otherValue;
      if (next === current) {
        setDraft(String(next));
        return;
      }

      const ownedQty = field === "owned" ? next : otherValue;
      const proxyQty = field === "proxy" ? next : otherValue;
      setDraft(String(next));
      setError(null);

      try {
        await mutation.mutateAsync({ printingId, ownedQty, proxyQty });
      } catch (err) {
        setError("Update failed");
        setDraft(String(current));
      }
    },
    [field, otherValue, printingId, value, mutation]
  );

  const handleBlur = useCallback(() => {
    void commit(draft);
  }, [commit, draft]);

  const handleInputChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
    setDraft(event.target.value);
  }, []);

  const handleKeyDown = useCallback(
    (event: React.KeyboardEvent<HTMLInputElement>) => {
      if (event.key === "Enter") {
        event.preventDefault();
        void commit((event.target as HTMLInputElement).value);
      }
    },
    [commit]
  );

  const applyDelta = useCallback(
    (delta: number) => {
      const current = clamp(Number(draft));
      const next = clamp(current + delta);
      setDraft(String(next));
      void commit(String(next));
    },
    [commit, draft]
  );

  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-center gap-1">
        <button
          type="button"
          className="h-8 w-8 rounded border border-input text-sm font-medium text-muted-foreground transition hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
          onClick={() => applyDelta(-1)}
          disabled={mutation.isPending || clamp(Number(draft)) <= MIN_QTY}
          aria-label="Decrease"
        >
          −
        </button>
        <input
          type="number"
          inputMode="numeric"
          pattern="[0-9]*"
          min={MIN_QTY}
          max={MAX_QTY}
          value={draft}
          onChange={handleInputChange}
          onBlur={handleBlur}
          onKeyDown={handleKeyDown}
          className="h-8 w-16 rounded border border-input bg-background px-2 text-center text-sm focus:outline-none focus:ring"
          aria-label={field === "owned" ? "Owned quantity" : "Proxy quantity"}
        />
        <button
          type="button"
          className="h-8 w-8 rounded border border-input text-sm font-medium text-muted-foreground transition hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
          onClick={() => applyDelta(1)}
          disabled={mutation.isPending || clamp(Number(draft)) >= MAX_QTY}
          aria-label="Increase"
        >
          +
        </button>
      </div>
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}
