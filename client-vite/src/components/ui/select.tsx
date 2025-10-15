import type { ReactNode } from "react";
import { createContext, useCallback, useContext, useEffect, useId, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";

type SelectContextValue = {
  value: string;
  open: boolean;
  disabled: boolean;
  setOpen: (open: boolean) => void;
  onSelect: (value: string, label: ReactNode) => void;
  selectedLabel: ReactNode | null;
  setSelectedLabel: (label: ReactNode | null) => void;
  triggerRef: React.RefObject<HTMLButtonElement>;
  triggerId: string;
  listboxId: string;
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
  const id = useId();
  const [open, setOpen] = useState(false);
  const [internalValue, setInternalValue] = useState(defaultValue);
  const [selectedLabel, setSelectedLabel] = useState<ReactNode | null>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  const currentValue = value ?? internalValue;
  const triggerId = `${id}-trigger`;
  const listboxId = `${id}-listbox`;

  useEffect(() => {
    if (value !== undefined) {
      setInternalValue(value);
      if (!value) setSelectedLabel(null);
    }
  }, [value]);

  useEffect(() => {
    if (!currentValue) setSelectedLabel(null);
  }, [currentValue]);

  // Close on Escape key
  useEffect(() => {
    if (!open) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        setOpen(false);
        triggerRef.current?.focus();
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [open]);

  const onSelect = useCallback(
    (next: string, label: ReactNode) => {
      if (value === undefined) {
        setInternalValue(next);
      }
      onValueChange?.(next);
      setSelectedLabel(label);
      setOpen(false);
      triggerRef.current?.focus();
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
      triggerRef,
      triggerId,
      listboxId,
    }),
    [currentValue, open, disabled, onSelect, selectedLabel, triggerId, listboxId]
  );

  return (
    <SelectContext.Provider value={contextValue}>
      <div className="relative inline-flex w-full flex-col">{children}</div>
    </SelectContext.Provider>
  );
}

type SelectTriggerProps = React.ButtonHTMLAttributes<HTMLButtonElement>;

export function SelectTrigger({ className = "", children, ...props }: SelectTriggerProps) {
  const { open, setOpen, disabled, triggerRef, triggerId, listboxId } = useSelectContext();
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
      ref={triggerRef}
      id={triggerId}
      type="button"
      role="combobox"
      aria-expanded={open}
      aria-haspopup="listbox"
      aria-controls={open ? listboxId : undefined}
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
  const { open, setOpen, triggerRef, triggerId, listboxId } = useSelectContext();
  const contentRef = useRef<HTMLDivElement>(null);
  const [position, setPosition] = useState<{ top: number; left: number; width: number } | null>(
    null
  );

  // Calculate position relative to trigger
  useEffect(() => {
    if (!open || !triggerRef.current) return;

    const updatePosition = () => {
      const trigger = triggerRef.current;
      if (!trigger) return;

      const rect = trigger.getBoundingClientRect();
      setPosition({
        top: rect.bottom + window.scrollY,
        left: rect.left + window.scrollX,
        width: rect.width,
      });
    };

    updatePosition();
    window.addEventListener("resize", updatePosition);
    window.addEventListener("scroll", updatePosition, true);

    return () => {
      window.removeEventListener("resize", updatePosition);
      window.removeEventListener("scroll", updatePosition, true);
    };
  }, [open, triggerRef]);

  // Handle keyboard navigation
  const hasBoundNavigation = useRef(false);

  useEffect(() => {
    if (!open) {
      hasBoundNavigation.current = false;
      return;
    }

    if (!contentRef.current || !position || hasBoundNavigation.current) {
      return;
    }

    const content = contentRef.current;
    if (!content) return;

    hasBoundNavigation.current = true;

    const handleKeyDown = (event: KeyboardEvent) => {
      const options = Array.from(content.querySelectorAll<HTMLButtonElement>('[role="option"]'));
      if (options.length === 0) return;

      const currentIndex = options.findIndex((opt) => opt === document.activeElement);

      if (event.key === "ArrowDown") {
        event.preventDefault();
        const nextIndex = currentIndex < options.length - 1 ? currentIndex + 1 : 0;
        options[nextIndex]?.focus();
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        const prevIndex = currentIndex > 0 ? currentIndex - 1 : options.length - 1;
        options[prevIndex]?.focus();
      } else if (event.key === "Home") {
        event.preventDefault();
        options[0]?.focus();
      } else if (event.key === "End") {
        event.preventDefault();
        options[options.length - 1]?.focus();
      }
    };

    content.addEventListener("keydown", handleKeyDown);

    // Focus first option when opening
    const focusTimeout = window.setTimeout(() => {
      const firstOption = content.querySelector<HTMLButtonElement>('[role="option"]');
      firstOption?.focus();
    }, 0);

    return () => {
      hasBoundNavigation.current = false;
      window.clearTimeout(focusTimeout);
      content.removeEventListener("keydown", handleKeyDown);
    };
  }, [open, position]);

  // Click outside to close
  useEffect(() => {
    if (!open) return;

    const handleClickOutside = (event: MouseEvent) => {
      if (
        contentRef.current &&
        !contentRef.current.contains(event.target as Node) &&
        triggerRef.current &&
        !triggerRef.current.contains(event.target as Node)
      ) {
        setOpen(false);
        triggerRef.current?.focus();
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open, setOpen, triggerRef]);

  if (!open || !position) return null;

  const classes = [
    "absolute max-h-60 overflow-auto rounded-md border border-input",
    "bg-white dark:bg-gray-900",
    "text-popover-foreground shadow-lg ring-1 ring-black/5",
    "z-50",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return createPortal(
    <div
      ref={contentRef}
      id={listboxId}
      role="listbox"
      aria-labelledby={triggerId}
      className={classes}
      style={{
        position: "absolute",
        top: `${position.top + 8}px`,
        left: `${position.left}px`,
        width: `${position.width}px`,
      }}
      {...props}
    >
      {children}
    </div>,
    document.body
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
    "hover:bg-accent hover:text-accent-foreground",
    isSelected ? "bg-accent text-accent-foreground" : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <button
      type="button"
      role="option"
      aria-selected={isSelected}
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
