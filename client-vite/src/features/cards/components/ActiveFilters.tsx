import React from "react";

interface Props {
  filters: Record<string, string | undefined>;
  onClear: (key?: string) => void;
}

export function ActiveFilters({ filters, onClear }: Props) {
  const active = Object.entries(filters).filter(([, value]) => {
    if (!value) return false;
    return value.trim().length > 0;
  });

  if (active.length === 0) return null;

  return (
    <div className="mb-4 flex flex-wrap gap-2">
      {active.map(([key, value]) => (
        <button
          key={key}
          type="button"
          onClick={() => onClear(key)}
          className="text-sm bg-muted px-2 py-1 rounded hover:bg-muted-foreground/10"
        >
          {key}: {value} ✕
        </button>
      ))}
      <button
        type="button"
        onClick={() => onClear(undefined)}
        className="text-sm underline text-muted-foreground ml-2"
      >
        Clear All
      </button>
    </div>
  );
}
