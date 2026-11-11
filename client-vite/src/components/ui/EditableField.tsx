import * as React from "react";

export type EditableFieldProps = {
  value: string;
  onSave: (nextValue: string) => void | Promise<void>;
  placeholder?: string;
  emptyPlaceholder?: string;
  multiline?: boolean;
  className?: string;
  textClassName?: string;
  inputClassName?: string;
  ariaLabel?: string;
  disabled?: boolean;
};

export function EditableField({
  value,
  onSave,
  placeholder,
  emptyPlaceholder = "Click to edit",
  multiline = false,
  className = "",
  textClassName = "",
  inputClassName = "",
  ariaLabel,
  disabled = false,
}: EditableFieldProps) {
  const [isEditing, setIsEditing] = React.useState(false);
  const [draft, setDraft] = React.useState(value);
  const inputRef = React.useRef<HTMLInputElement | HTMLTextAreaElement | null>(null);

  React.useEffect(() => {
    if (!isEditing) {
      setDraft(value);
    }
  }, [value, isEditing]);

  React.useEffect(() => {
    if (isEditing && inputRef.current) {
      inputRef.current.focus();
      inputRef.current.select();
    }
  }, [isEditing]);

  const handleCommit = () => {
    setIsEditing(false);
    const trimmed = draft.trim();
    if (trimmed !== value.trim()) {
      onSave(trimmed);
    }
  };

  const handleCancel = () => {
    setDraft(value);
    setIsEditing(false);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Escape") {
      e.preventDefault();
      handleCancel();
    } else if (e.key === "Enter") {
      if (!multiline || !e.shiftKey) {
        e.preventDefault();
        handleCommit();
      }
    }
  };

  if (isEditing && !disabled) {
    if (multiline) {
      return (
        <div className={`relative w-full ${className}`}>
          <textarea
            ref={inputRef as React.RefObject<HTMLTextAreaElement>}
            aria-label={ariaLabel}
            value={draft}
            placeholder={placeholder}
            rows={3}
            onChange={(e) => setDraft(e.target.value)}
            onBlur={handleCommit}
            onKeyDown={handleKeyDown}
            className={`w-full rounded-md border border-sky-500 bg-zinc-900 px-2 py-1 text-sm text-zinc-100 shadow-inner focus:outline-none focus:ring-1 focus:ring-sky-500 resize-none ${inputClassName}`}
          />
          <div className="mt-0.5 text-[11px] text-zinc-400">
            Press <kbd className="px-1 py-0.5 rounded bg-zinc-800 text-zinc-300">Enter</kbd> to
            save, <kbd className="px-1 py-0.5 rounded bg-zinc-800 text-zinc-300">Shift+Enter</kbd>{" "}
            for new line, <kbd className="px-1 py-0.5 rounded bg-zinc-800 text-zinc-300">Esc</kbd>{" "}
            to cancel
          </div>
        </div>
      );
    }

    return (
      <div className={`relative w-full ${className}`}>
        <input
          ref={inputRef as React.RefObject<HTMLInputElement>}
          type="text"
          aria-label={ariaLabel}
          value={draft}
          placeholder={placeholder}
          onChange={(e) => setDraft(e.target.value)}
          onBlur={handleCommit}
          onKeyDown={handleKeyDown}
          className={`w-full rounded-md border border-sky-500 bg-zinc-900 px-2 py-1 text-sm text-zinc-100 shadow-inner focus:outline-none focus:ring-1 focus:ring-sky-500 ${inputClassName}`}
        />
      </div>
    );
  }

  const hasValue = Boolean(value && value.trim().length > 0);

  return (
    <div
      className={`group inline-flex items-center gap-1.5 cursor-pointer rounded px-1.5 py-0.5 hover:bg-zinc-800/60 transition-colors ${className}`}
      onClick={() => !disabled && setIsEditing(true)}
      title="Click or press icon to edit"
      role="button"
      tabIndex={disabled ? -1 : 0}
      onKeyDown={(e) => {
        if (!disabled && (e.key === "Enter" || e.key === " ")) {
          e.preventDefault();
          setIsEditing(true);
        }
      }}
    >
      <span className={`${hasValue ? "text-zinc-100" : "italic text-zinc-500"} ${textClassName}`}>
        {hasValue ? value : emptyPlaceholder}
      </span>
      {!disabled && (
        <button
          type="button"
          aria-label={ariaLabel ? `Edit ${ariaLabel}` : "Edit"}
          onClick={(e) => {
            e.stopPropagation();
            setIsEditing(true);
          }}
          className="p-1 rounded text-zinc-400 opacity-60 group-hover:opacity-100 hover:text-sky-400 hover:bg-zinc-700/50 transition-all focus:opacity-100 focus:outline-none"
        >
          <svg
            className="w-3.5 h-3.5"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={2}
            aria-hidden="true"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z"
            />
          </svg>
        </button>
      )}
    </div>
  );
}
