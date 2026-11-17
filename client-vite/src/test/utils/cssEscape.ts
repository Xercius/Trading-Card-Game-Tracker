/**
 * Escapes a CSS selector ID to handle special characters like colons.
 * Uses native CSS.escape if available, otherwise falls back to manual escaping.
 */
export function cssEscapeId(id: string): string {
  // Use native CSS.escape if available
  if (typeof CSS !== "undefined" && CSS.escape) {
    return CSS.escape(id);
  }

  // Fallback: escape special characters manually
  // Escape backslashes and colons first
  let escaped = id.replace(/\\/g, "\\\\").replace(/:/g, "\\:");

  // Escape leading digits with \3X (hex code)
  if (/^\d/.test(escaped)) {
    const firstChar = escaped.charCodeAt(0).toString(16);
    escaped = `\\3${firstChar.slice(-1)} ${escaped.slice(1)}`;
  }

  return escaped;
}
