import * as React from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

type MultiSelectProps = {
  label: string;
  values: string[];
  options: string[];
  placeholder?: string;
  onChange: (values: string[]) => void;
};

type IconProps = { className?: string };

function ChevronDownIcon({ className = "" }: IconProps) {
  return (
    <svg
      aria-hidden="true"
      className={`h-4 w-4 ${className}`}
      viewBox="0 0 20 20"
      fill="none"
      stroke="currentColor"
    >
      <path d="M6 8l4 4 4-4" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function CheckIcon({ className = "" }: IconProps) {
  return (
    <svg
      aria-hidden="true"
      className={`h-4 w-4 ${className}`}
      viewBox="0 0 20 20"
      fill="none"
      stroke="currentColor"
    >
      <path d="M5 10l3.5 3.5L15 7" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function XIcon({ className = "" }: IconProps) {
  return (
    <svg aria-hidden="true" className={`h-3 w-3 ${className}`} viewBox="0 0 20 20" fill="none" stroke="currentColor">
      <path d="M6 6l8 8M14 6l-8 8" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function MultiSelect({
  label,
  values,
  options,
  placeholder = "Search…",
  onChange,
}: MultiSelectProps) {
  const containerRef = React.useRef<HTMLDivElement | null>(null);
  const inputRef = React.useRef<HTMLInputElement | null>(null);
  const [open, setOpen] = React.useState(false);
  const [search, setSearch] = React.useState("");

  const summary = values.length > 0 ? `${label} (${values.length})` : `${label} (All)`;

  const normalizedSearch = search.trim().toLowerCase();
  const filteredOptions = React.useMemo(() => {
    if (!normalizedSearch) return options;
    return options.filter((option) => option.toLowerCase().includes(normalizedSearch));
  }, [normalizedSearch, options]);

  const toggleValue = React.useCallback(
    (value: string) => {
      if (values.includes(value)) {
        onChange(values.filter((item) => item !== value));
      } else {
        onChange([...values, value]);
      }
    },
    [onChange, values]
  );

  const removeValue = React.useCallback(
    (value: string) => {
      if (!values.includes(value)) return;
      onChange(values.filter((item) => item !== value));
    },
    [onChange, values]
  );

  React.useEffect(() => {
    if (!open) return;
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  React.useEffect(() => {
    if (!open) return;
    setSearch("");
    const frame = requestAnimationFrame(() => {
      inputRef.current?.focus();
    });
    return () => cancelAnimationFrame(frame);
  }, [open]);

  return (
    <div className="space-y-2">
      <div ref={containerRef} className="relative">
        <Button
          variant="outline"
          className="flex w-56 items-center justify-between truncate"
          aria-expanded={open}
          aria-haspopup="listbox"
          onClick={() => setOpen((prev) => !prev)}
        >
          <span className="truncate text-left text-sm font-medium">{summary}</span>
          <ChevronDownIcon className={open ? "-rotate-180 transition-transform" : "transition-transform"} />
        </Button>
        {open && (
          <div className="absolute left-0 z-50 mt-2 w-72 rounded-md border border-input bg-white dark:bg-gray-900 shadow-lg">
            <div className="border-b border-input px-3 py-2">
              <input
                ref={inputRef}
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder={placeholder}
                className="w-full rounded-md border border-input bg-background px-2 py-1 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                type="search"
              />
            </div>
            <div
              role="listbox"
              aria-label={label}
              aria-multiselectable="true"
              className="max-h-60 overflow-auto py-1 text-sm"
            >
              {filteredOptions.length === 0 ? (
                <div className="px-3 py-2 text-muted-foreground">No results.</div>
              ) : (
                filteredOptions.map((option) => {
                  const selected = values.includes(option);
                  return (
                    <button
                      key={option}
                      type="button"
                      role="option"
                      aria-selected={selected}
                      onClick={() => toggleValue(option)}
                      className={`flex w-full items-center justify-between px-3 py-2 text-left hover:bg-accent hover:text-accent-foreground ${
                        selected ? "font-semibold" : ""
                      }`}
                    >
                      <span className="truncate">{option}</span>
                      <CheckIcon className={selected ? "text-accent-foreground" : "opacity-0"} />
                    </button>
                  );
                })
              )}
            </div>
          </div>
        )}
      </div>
      <div className="flex flex-wrap gap-2">
        {values.map((value) => (
          <Badge key={value} variant="secondary" className="gap-1">
            <span>{value}</span>
            <button
              type="button"
              aria-label={`Remove ${value}`}
              className="ml-1 inline-flex items-center justify-center rounded-full p-0.5 hover:bg-secondary/80"
              onClick={() => removeValue(value)}
            >
              <XIcon />
            </button>
          </Badge>
        ))}
      </div>
    </div>
  );
}
