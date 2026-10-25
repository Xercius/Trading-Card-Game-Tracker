import * as React from "react";

type Debounced<Args extends unknown[]> = ((...args: Args) => void) & {
  cancel: () => void;
  flush: () => void;
};

export function useDebouncedCallback<Args extends unknown[]>(
  callback: (...args: Args) => void,
  delay: number
): Debounced<Args> {
  const callbackRef = React.useRef(callback);
  const argsRef = React.useRef<Args | undefined>(undefined);
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

  const debounced = React.useCallback(
    (...args: Args) => {
      argsRef.current = args;
      cancel();
      timeoutRef.current = window.setTimeout(() => {
        timeoutRef.current = undefined;
        if (argsRef.current) {
          callbackRef.current(...argsRef.current);
        }
      }, delay);
    },
    [cancel, delay]
  );

  React.useEffect(() => cancel, [cancel]);

  return Object.assign(debounced, { cancel, flush });
}
