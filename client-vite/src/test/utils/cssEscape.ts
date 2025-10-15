/**
 * Escape an ID string for use in CSS selectors.
 * Uses native CSS.escape when available, otherwise provides a minimal fallback
 * for common special characters like colons and leading digits.
 *
 * @param id - The ID string to escape
 * @returns The escaped ID string safe for use in CSS selectors
 *
 * @example
 * ```ts
 * const el = document.querySelector(`#${cssEscapeId('login-username')}`);
 * const el2 = document.querySelector(`#${cssEscapeId('item:123')}`);
 * ```
 */
export function cssEscapeId(id: string): string {
  // Use native CSS.escape when available
  if ((globalThis as any).CSS?.escape) {
    return (globalThis as any).CSS.escape(id);
  }

  // Minimal fallback for colons, leading digits, and leading hyphen before digit
  return id
    .replace(/^-(?=\d)/, '\\-')
    .replace(/^[0-9]/, '\\3$& ')
    .replace(/:/g, '\\:');
}
