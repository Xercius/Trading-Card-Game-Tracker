import { useEffect, useRef, useState } from "react";

type QuantityStepperProps = {
  value: number;
  disabled?: boolean;
  min?: number;
  max?: number;
  debounceMs?: number;
  textColorClass?: string;
  ariaLabel: string;
  onCommit: (nextValue: number) => void;
};

/**
 * Non-blocking quantity stepper that updates immediately in the UI and coalesces
 * rapid +/- clicks or typed input before persisting to the server.
 */
export function QuantityStepper({
  value,
  disabled = false,
  min = 0,
  max = 999,
  debounceMs = 250,
  textColorClass = "text-zinc-100",
  ariaLabel,
  onCommit,
}: QuantityStepperProps) {
  const [localValue, setLocalValue] = useState<number>(value);
  const [inputText, setInputText] = useState<string>(String(value));
  const [isEditing, setIsEditing] = useState(false);

  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pendingValueRef = useRef<number | null>(null);
  const onCommitRef = useRef(onCommit);
  onCommitRef.current = onCommit;

  useEffect(() => {
    if (!isEditing && pendingValueRef.current === null) {
      setLocalValue(value);
      setInputText(String(value));
    }
  }, [value, isEditing]);

  const clearScheduledCommit = () => {
    if (timerRef.current !== null) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  };

  const flushPending = () => {
    clearScheduledCommit();
    if (pendingValueRef.current !== null) {
      const next = pendingValueRef.current;
      pendingValueRef.current = null;
      onCommitRef.current(next);
    }
  };

  useEffect(() => {
    return () => {
      if (timerRef.current !== null && pendingValueRef.current !== null) {
        clearTimeout(timerRef.current);
        const next = pendingValueRef.current;
        pendingValueRef.current = null;
        onCommitRef.current(next);
      }
    };
  }, []);

  const clamp = (n: number) => Math.max(min, Math.min(max, n));

  const scheduleStepCommit = (next: number) => {
    const clamped = clamp(next);
    setLocalValue(clamped);
    setInputText(String(clamped));
    pendingValueRef.current = clamped;

    clearScheduledCommit();
    timerRef.current = setTimeout(() => {
      timerRef.current = null;
      if (pendingValueRef.current !== null) {
        const committed = pendingValueRef.current;
        pendingValueRef.current = null;
        onCommitRef.current(committed);
      }
    }, debounceMs);
  };

  const commitTypedValue = () => {
    setIsEditing(false);
    const parsed = Number.parseInt(inputText, 10);
    const clamped = Number.isNaN(parsed) ? min : clamp(parsed);
    setLocalValue(clamped);
    setInputText(String(clamped));
    clearScheduledCommit();
    pendingValueRef.current = null;
    if (clamped !== value) {
      onCommitRef.current(clamped);
    }
  };

  return (
    <div className="inline-flex items-center rounded-lg border border-zinc-700/70 bg-zinc-950/80 p-0.5 shadow-inner">
      <button
        type="button"
        aria-label={`Decrease ${ariaLabel}`}
        onClick={() => scheduleStepCommit(localValue - 1)}
        disabled={disabled || localValue <= min}
        className="h-6 w-6 rounded text-zinc-400 hover:bg-zinc-800 hover:text-white disabled:opacity-30 disabled:hover:bg-transparent"
      >
        −
      </button>
      <input
        type="number"
        aria-label={ariaLabel}
        min={min}
        max={max}
        value={inputText}
        disabled={disabled}
        onFocus={() => setIsEditing(true)}
        onChange={(e) => {
          setIsEditing(true);
          setInputText(e.target.value);
          const parsed = Number.parseInt(e.target.value, 10);
          if (!Number.isNaN(parsed)) {
            setLocalValue(clamp(parsed));
          }
        }}
        onBlur={() => {
          if (isEditing) {
            commitTypedValue();
          } else {
            flushPending();
          }
        }}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            e.currentTarget.blur();
          }
        }}
        className={`w-8 text-center text-xs font-bold ${textColorClass} bg-transparent focus:outline-none disabled:opacity-50 [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none`}
      />
      <button
        type="button"
        aria-label={`Increase ${ariaLabel}`}
        onClick={() => scheduleStepCommit(localValue + 1)}
        disabled={disabled || localValue >= max}
        className="h-6 w-6 rounded text-zinc-400 hover:bg-zinc-800 hover:text-white disabled:opacity-30 disabled:hover:bg-transparent"
      >
        +
      </button>
    </div>
  );
}
