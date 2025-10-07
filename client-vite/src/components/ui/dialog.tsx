import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useRef,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";

const DialogContext = createContext<{
  open: boolean;
  onOpenChange: (open: boolean) => void;
} | null>(null);

export type DialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  children: ReactNode;
};

export function Dialog({ open, onOpenChange, children }: DialogProps) {
  const value = useMemo(() => ({ open, onOpenChange }), [open, onOpenChange]);
  return <DialogContext.Provider value={value}>{children}</DialogContext.Provider>;
}

function useDialogContext(component: string) {
  const ctx = useContext(DialogContext);
  if (!ctx) throw new Error(`${component} must be used within <Dialog>`);
  return ctx;
}

const FOCUSABLE = [
  "a[href]",
  "button:not([disabled])",
  "textarea:not([disabled])",
  "input:not([disabled])",
  "select:not([disabled])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

export type DialogContentProps = {
  className?: string;
  children: ReactNode;
  labelledBy?: string;
  describedBy?: string;
};

export function DialogContent({ className = "", children, labelledBy, describedBy }: DialogContentProps) {
  const { open, onOpenChange } = useDialogContext("DialogContent");
  const contentRef = useRef<HTMLDivElement | null>(null);
  const lastFocused = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;
    lastFocused.current = (document.activeElement as HTMLElement) ?? null;
    const frame = requestAnimationFrame(() => {
      const content = contentRef.current;
      if (!content) return;
      const focusables = Array.from(content.querySelectorAll<HTMLElement>(FOCUSABLE));
      (focusables[0] ?? content).focus();
    });
    return () => cancelAnimationFrame(frame);
  }, [open]);

  useEffect(() => {
    if (open) return () => {};
    const prev = lastFocused.current;
    if (prev && typeof prev.focus === "function") {
      prev.focus();
    }
    return () => {};
  }, [open]);

  useEffect(() => {
    if (!open) return undefined;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onOpenChange(false);
        return;
      }
      if (event.key !== "Tab") return;
      const content = contentRef.current;
      if (!content) return;
      const focusables = Array.from(content.querySelectorAll<HTMLElement>(FOCUSABLE));
      if (focusables.length === 0) {
        event.preventDefault();
        content.focus();
        return;
      }
      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      const active = document.activeElement as HTMLElement | null;
      if (!event.shiftKey && active === last) {
        event.preventDefault();
        first.focus();
      } else if (event.shiftKey && active === first) {
        event.preventDefault();
        last.focus();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [open, onOpenChange]);

  if (!open) return null;

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-center justify-center"
      role="presentation"
    >
      <div className="absolute inset-0 bg-black/50" aria-hidden="true" onClick={() => onOpenChange(false)} />
      <div
        ref={contentRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={labelledBy}
        aria-describedby={describedBy}
        tabIndex={-1}
        className={`relative z-10 max-h-[90vh] w-full max-w-4xl overflow-hidden rounded-2xl bg-background shadow-lg focus:outline-none ${className}`}
      >
        {children}
      </div>
    </div>,
    document.body
  );
}

export function DialogHeader({ className = "", children }: { className?: string; children: ReactNode }) {
  return <div className={`space-y-1.5 border-b px-6 pb-4 pt-6 ${className}`}>{children}</div>;
}

export function DialogTitle({ className = "", children, id }: { className?: string; children: ReactNode; id?: string }) {
  return (
    <h2 id={id} className={`text-xl font-semibold leading-none tracking-tight ${className}`}>
      {children}
    </h2>
  );
}

export function DialogDescription({ className = "", children, id }: { className?: string; children: ReactNode; id?: string }) {
  return (
    <p id={id} className={`text-sm text-muted-foreground ${className}`}>
      {children}
    </p>
  );
}

export function DialogFooter({ className = "", children }: { className?: string; children: ReactNode }) {
  return <div className={`flex flex-col gap-2 border-t px-6 py-4 sm:flex-row sm:justify-end ${className}`}>{children}</div>;
}

export function DialogClose({
  className = "",
  children,
  "aria-label": ariaLabel = "Close dialog",
}: {
  className?: string;
  children?: ReactNode;
  "aria-label"?: string;
}) {
  const { onOpenChange, open } = useDialogContext("DialogClose");
  if (!open) return null;
  return (
    <button
      type="button"
      aria-label={ariaLabel}
      className={`absolute right-4 top-4 inline-flex h-8 w-8 items-center justify-center rounded-full text-muted-foreground transition hover:bg-muted ${className}`}
      onClick={() => onOpenChange(false)}
    >
      {children ?? <span aria-hidden="true">×</span>}
    </button>
  );
}
