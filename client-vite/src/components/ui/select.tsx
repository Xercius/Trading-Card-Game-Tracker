import type { ReactNode } from "react";
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";

type SelectContextValue = {
  value: string;
  open: boolean;
  disabled: boolean;
  setOpen: (open: boolean) => void;
  onSelect: (value: string, label: ReactNode) => void;
  selectedLabel: ReactNode | null;
  setSelectedLabel: (label: ReactNode | null) => void;
};

const SelectContext = createContext<SelectContextValue | null>(null);

function useSelectContext() {
  const ctx = useContext(SelectContext);
  if (!ctx) throw new Error("Select components must be used within <Select>");
  return ctx;
}

type SelectProps = {
  value?: string;
  defaultValue?: string;
  onValueChange?: (value: string) => void;
  disabled?: boolean;
  children: ReactNode;
};

export function Select({
  value,
  defaultValue = "",
  onValueChange,
  disabled = false,
  children,
}: SelectProps) {
  const [open, setOpen] = useState(false);
  const [internalValue, setInternalValue] = useState(defaultValue);
  const [selectedLabel, setSelectedLabel] = useState<ReactNode | null>(null);

  const currentValue = value ?? internalValue;

  useEffect(() => {
    if (value !== undefined) {
      setInternalValue(value);
      if (!value) setSelectedLabel(null);
    }
  }, [value]);

  useEffect(() => {
    if (!currentValue) setSelectedLabel(null);
  }, [currentValue]);

  const onSelect = useCallback(
    (next: string, label: ReactNode) => {
      if (value === undefined) {
        setInternalValue(next);
      }
      onValueChange?.(next);
      setSelectedLabel(label);
      setOpen(false);
    },
    [value, onValueChange]
  );

  const contextValue = useMemo<SelectContextValue>(
    () => ({
      value: currentValue ?? "",
      open,
      disabled,
      setOpen,
      onSelect,
      selectedLabel,
      setSelectedLabel,
    }),
    [currentValue, open, disabled, onSelect, selectedLabel]
  );

  return (
    <SelectContext.Provider value={contextValue}>
      <div className="relative inline-flex w-full flex-col">{children}</div>
    </SelectContext.Provider>
  );
}

type SelectTriggerProps = React.ButtonHTMLAttributes<HTMLButtonElement>;

export function SelectTrigger({ className = "", children, ...props }: SelectTriggerProps) {
  const { open, setOpen, disabled } = useSelectContext();
  const classes = [
    "flex h-9 w-full items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm",
    "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
    "disabled:cursor-not-allowed disabled:opacity-50",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <button
      type="button"
      role="combobox"
      aria-expanded={open}
      data-state={open ? "open" : "closed"}
      className={classes}
      disabled={disabled}
      onClick={() => !disabled && setOpen(!open)}
      {...props}
    >
      {children}
      <svg
        aria-hidden="true"
        className="ml-2 h-4 w-4 shrink-0 opacity-70"
        viewBox="0 0 20 20"
        fill="none"
        stroke="currentColor"
      >
        <path d="M6 8l4 4 4-4" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </button>
  );
}

type SelectValueProps = {
  placeholder?: ReactNode;
  className?: string;
};

export function SelectValue({ placeholder, className = "" }: SelectValueProps) {
  const { selectedLabel, value } = useSelectContext();
  const showPlaceholder = !selectedLabel && !value;
  const classes = [showPlaceholder ? "text-muted-foreground" : "", className]
    .filter(Boolean)
    .join(" ");
  return (
    <span className={classes}>{selectedLabel ?? (showPlaceholder ? placeholder : value)}</span>
  );
}

type SelectContentProps = React.HTMLAttributes<HTMLDivElement>;

export function SelectContent({ className = "", children, ...props }: SelectContentProps) {
  const { open } = useSelectContext();
  if (!open) return null;
  const classes = [
    "absolute top-full z-50 mt-2 max-h-60 w-full overflow-auto rounded-md border border-input bg-white dark:bg-gray-900",
    "shadow-lg",
    className,
  ]
    .filter(Boolean)
    .join(" ");
  return (
    <div className={classes} {...props}>
      {children}
    </div>
  );
}

type SelectItemProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  value: string;
};

export function SelectItem({ value, className = "", children, ...props }: SelectItemProps) {
  const { value: selectedValue, onSelect, setSelectedLabel } = useSelectContext();
  const isSelected = selectedValue === value;

  useEffect(() => {
    if (isSelected) {
      setSelectedLabel(children);
    }
  }, [isSelected, children, setSelectedLabel]);

  const classes = [
    "relative flex w-full cursor-pointer select-none items-center rounded-sm px-3 py-2 text-sm",
    "outline-none focus:bg-accent focus:text-accent-foreground",
    isSelected ? "bg-accent text-accent-foreground" : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <button
      type="button"
      role="option"
      data-state={isSelected ? "checked" : "unchecked"}
      className={classes}
      onClick={() => onSelect(value, children)}
      {...props}
    >
      {children}
    </button>
  );
}

export { useSelectContext };
