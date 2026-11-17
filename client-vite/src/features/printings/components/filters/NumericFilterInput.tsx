import { useEffect, useState } from "react";
import { Input } from "@/components/ui/input";

export type NumericFilterInputProps = {
  id: string;
  label: string;
  values?: string[];
  placeholder?: string;
  labelWidthClass?: string;
  onCommit: (values: string[]) => void;
};

/**
 * Controlled numeric filter input supporting exact numbers and > / < comparison operators.
 */
export function NumericFilterInput({
  id,
  label,
  values,
  placeholder = "e.g. >2 or 3",
  labelWidthClass = "w-24",
  onCommit,
}: NumericFilterInputProps) {
  const [draft, setDraft] = useState(() => values?.join(", ") ?? "");

  useEffect(() => {
    setDraft(values?.join(", ") ?? "");
  }, [values]);

  const commit = () => {
    const parsed = draft
      .split(",")
      .map((v) => v.trim())
      .filter(Boolean);
    onCommit(parsed);
  };

  return (
    <div className="flex items-center gap-2">
      <label
        htmlFor={id}
        className={`text-xs font-semibold text-muted-foreground ${labelWidthClass} shrink-0`}
      >
        {label}
      </label>
      <Input
        id={id}
        placeholder={placeholder}
        className="h-8 text-xs bg-card/40 border-white/20"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            commit();
          }
        }}
      />
    </div>
  );
}
