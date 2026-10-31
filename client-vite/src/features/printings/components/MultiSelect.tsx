import * as React from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

type MultiSelectProps = {
  label: string;
  values: string[];
  options: string[];
  placeholder?: string;
  onChange: (values: string[]) => void;
  className?: string;
  method?: "or" | "and" | "exclude";
  onMethodChange?: (method: "or" | "and" | "exclude") => void;
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
    <svg
      aria-hidden="true"
      className={`h-3 w-3 ${className}`}
      viewBox="0 0 20 20"
      fill="none"
      stroke="currentColor"
    >
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
  className,
  method,
  onMethodChange,
}: MultiSelectProps) {
  const containerRef = React.useRef<HTMLDivElement | null>(null);
  const inputRef = React.useRef<HTMLInputElement | null>(null);
  const [open, setOpen] = React.useState(false);
  const [search, setSearch] = React.useState("");

  const summary = React.useMemo(() => {
    if (values.length === 0) return label;
    if (method && method !== "or") {
      const prefix = method === "exclude" ? "Exclude: " : "AND: ";
      return `${label} (${prefix}${values.length})`;
    }
    return `${label} (${values.length})`;
  }, [label, method, values.length]);

  const normalizedSearch = search.trim().toLowerCase();
  const filteredOptions = React.useMemo(() => {
    if (!normalizedSearch) return options;
    return options.filter((option) => option.toLowerCase().includes(normalizedSearch));
  }, [normalizedSearch, options]);

  const toggleValue = React.useCallback(
    (value: string) => {
      if (values.includes(value)) {
        const next = values.filter((item) => item !== value);
        onChange(next);
        if (next.length === 0 && onMethodChange) {
          onMethodChange("or");
        }
      } else {
        onChange([...values, value]);
      }
    },
    [onChange, onMethodChange, values]
  );

  const removeValue = React.useCallback(
    (value: string) => {
      if (!values.includes(value)) return;
      const next = values.filter((item) => item !== value);
      onChange(next);
      if (next.length === 0 && onMethodChange) {
        onMethodChange("or");
      }
    },
    [onChange, onMethodChange, values]
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
    <div>
      <div ref={containerRef} className="relative">
        <Button
          variant="outline"
          className={`flex items-center justify-between truncate ${className ?? "w-56"}`}
          aria-expanded={open}
          aria-haspopup="listbox"
          onClick={() => setOpen((prev) => !prev)}
        >
          <span className="truncate text-left text-sm font-medium">{summary}</span>
          <ChevronDownIcon
            className={open ? "-rotate-180 transition-transform" : "transition-transform"}
          />
        </Button>
        {open && (
          <div
            className={`absolute left-0 z-50 mt-2 rounded-lg border border-white/20 bg-[#242424] shadow-xl ${
              className?.includes("w-full") ? "w-full" : "w-72"
            }`}
          >
            {method && onMethodChange && (
              <div className="border-b border-white/10 p-2.5">
                <div className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-1.5">
                  Filter Method
                </div>
                <div
                  className="flex rounded-md border border-white/20 bg-black/30 p-0.5"
                  role="group"
                  aria-label={`${label} Filter Method`}
                >
                  {(["or", "and", "exclude"] as const).map((m) => {
                    const isActive = method === m;
                    return (
                      <button
                        key={m}
                        type="button"
                        aria-pressed={isActive}
                        onClick={() => onMethodChange(m)}
                        className={`flex-1 py-1 px-2 text-xs font-semibold rounded transition-colors ${
                          isActive
                            ? m === "exclude"
                              ? "bg-red-600 text-white shadow-sm"
                              : "bg-white/20 text-white shadow-sm"
                            : "text-muted-foreground hover:text-white hover:bg-white/5"
                        }`}
                      >
                        {m.toUpperCase()}
                      </button>
                    );
                  })}
                </div>
              </div>
            )}
            <div className="border-b border-input px-3 py-2">
              <input
                ref={inputRef}
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder={placeholder}
                className="w-full rounded-md border border-input bg-[#242424] px-2 py-1 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
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
      {values.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-2">
          {values.map((value) => (
            <Badge
              key={value}
              variant={method === "exclude" ? "destructive" : "secondary"}
              className="gap-1"
            >
              <span>{method === "exclude" ? `NOT ${value}` : value}</span>
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
      )}
    </div>
  );
}
