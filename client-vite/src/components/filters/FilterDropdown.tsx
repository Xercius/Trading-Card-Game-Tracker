import { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";

type FilterDropdownProps = {
  trigger: React.ReactNode;
  children: React.ReactNode;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  align?: "left" | "right";
};

/**
 * FilterDropdown - A dropdown menu for filters with proper layering and accessibility
 *
 * Features:
 * - Portal rendering to avoid stacking context issues
 * - Proper z-index above content
 * - Opaque background with shadow
 * - Keyboard navigation (Esc to close, Arrow keys)
 * - Focus management
 * - Click outside to close
 * - Responsive positioning
 */
export default function FilterDropdown({
  trigger,
  children,
  open: controlledOpen,
  onOpenChange,
  align = "left",
}: FilterDropdownProps) {
  const id = useId();
  const [internalOpen, setInternalOpen] = useState(false);
  const isControlled = controlledOpen !== undefined;
  const open = isControlled ? controlledOpen : internalOpen;

  const handleSetOpen = (value: boolean) => {
    if (isControlled && onOpenChange) onOpenChange(value);
    else if (!isControlled) setInternalOpen(value);
  };

  const triggerRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const [position, setPosition] = useState<{ top: number; left: number; right?: number } | null>(
    null
  );

  const triggerId = `${id}-trigger`;
  const menuId = `${id}-menu`;

  // Calculate position relative to trigger
  useEffect(() => {
    if (!open || !triggerRef.current) return;

    const updatePosition = () => {
      const trigger = triggerRef.current;
      if (!trigger) return;

      const rect = trigger.getBoundingClientRect();
      const pos: { top: number; left: number; right?: number } = {
        top: rect.bottom + window.scrollY,
        left: align === "left" ? rect.left + window.scrollX : 0,
      };

      if (align === "right") {
        pos.right = window.innerWidth - (rect.right + window.scrollX);
      }

      setPosition(pos);
    };

    updatePosition();
    window.addEventListener("resize", updatePosition);
    window.addEventListener("scroll", updatePosition, true);

    return () => {
      window.removeEventListener("resize", updatePosition);
      window.removeEventListener("scroll", updatePosition, true);
    };
  }, [open, align]);

  // Close on Escape key
  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (!contentRef.current) return;
      const focusables = contentRef.current.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
      );
      const focusableArray = Array.from(focusables);
      const activeElement = document.activeElement as HTMLElement;
      const currentIndex = focusableArray.indexOf(activeElement);

      if (event.key === "Escape") {
        event.preventDefault();
        handleSetOpen(false);
        triggerRef.current?.querySelector<HTMLElement>('[role="button"]')?.focus();
        return;
      }

      if (focusableArray.length === 0) return;

      if (event.key === "ArrowDown") {
        event.preventDefault();
        const nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % focusableArray.length;
        focusableArray[nextIndex].focus();
        return;
      }

      if (event.key === "ArrowUp") {
        event.preventDefault();
        const prevIndex =
          currentIndex < 0
            ? focusableArray.length - 1
            : (currentIndex - 1 + focusableArray.length) % focusableArray.length;
        focusableArray[prevIndex].focus();
        return;
      }

      if (event.key === "Home") {
        event.preventDefault();
        focusableArray[0].focus();
        return;
      }

      if (event.key === "End") {
        event.preventDefault();
        focusableArray[focusableArray.length - 1].focus();
        return;
      }

      // Trap Tab/Shift+Tab within the menu
      if (event.key === "Tab") {
        if (focusableArray.length === 0) return;
        event.preventDefault();
        let nextIndex;
        if (event.shiftKey) {
          nextIndex = currentIndex <= 0 ? focusableArray.length - 1 : currentIndex - 1;
        } else {
          nextIndex = currentIndex === focusableArray.length - 1 ? 0 : currentIndex + 1;
        }
        focusableArray[nextIndex].focus();
        return;
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [open, handleSetOpen]);

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
        handleSetOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open, handleSetOpen]);

  // Focus management
  useEffect(() => {
    if (!open || !contentRef.current) return;

    const content = contentRef.current;
    const focusables = content.querySelectorAll<HTMLElement>(
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    );

    if (focusables.length > 0) {
      setTimeout(() => focusables[0]?.focus(), 0);
    }
  }, [open]);

  const handleTriggerClick = () => {
    handleSetOpen(!open);
  };

  const positionStyle = position
    ? {
        position: "absolute" as const,
        top: `${position.top + 8}px`,
        left: align === "left" && position.left ? `${position.left}px` : undefined,
        right:
          align === "right" && position.right !== undefined ? `${position.right}px` : undefined,
        minWidth: "200px",
      }
    : undefined;

  return (
    <>
      <button
        ref={triggerRef}
        id={triggerId}
        type="button"
        aria-haspopup="menu"
        aria-controls={menuId}
        aria-expanded={open}
        onClick={handleTriggerClick}
      >
        {trigger}
      </button>

      {open &&
        position &&
        createPortal(
          <div
            ref={contentRef}
            id={menuId}
            role="menu"
            aria-labelledby={triggerId}
            className="rounded-md border border-input bg-white dark:bg-gray-900 shadow-lg ring-1 ring-black/5 z-50 max-h-[400px] overflow-auto"
            style={positionStyle}
          >
            {children}
          </div>,
          document.body
        )}
    </>
  );
}
