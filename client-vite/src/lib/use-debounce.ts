import * as React from "react";

type Debounced<T extends (...args: any[]) => void> = ((
  ...args: Parameters<T>
) => void) & {
  cancel: () => void;
  flush: () => void;
};

export function useDebouncedCallback<T extends (...args: any[]) => void>(
  callback: T,
  delay: number,
): Debounced<T> {
  const callbackRef = React.useRef(callback);
  const argsRef = React.useRef<Parameters<T> | undefined>(undefined);
  const timeoutRef = React.useRef<number | undefined>(undefined);

  React.useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);

  const cancel = React.useCallback(() => {
    if (timeoutRef.current != null) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = undefined;
    }
  }, []);

  const flush = React.useCallback(() => {
    if (timeoutRef.current != null) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = undefined;
      if (argsRef.current) {
        callbackRef.current(...argsRef.current);
      }
    }
  }, []);

  const debounced = React.useCallback((...args: Parameters<T>) => {
    argsRef.current = args;
    cancel();
    timeoutRef.current = window.setTimeout(() => {
      timeoutRef.current = undefined;
      if (argsRef.current) {
        callbackRef.current(...argsRef.current);
      }
    }, delay);
  }, [cancel, delay]);

  React.useEffect(() => cancel, [cancel]);

  return Object.assign(debounced, { cancel, flush });
}
