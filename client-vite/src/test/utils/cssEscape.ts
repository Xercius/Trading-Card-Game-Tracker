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
  // Escape leading digits with \3X (hex code)
  if (/^\d/.test(id)) {
    const firstChar = id.charCodeAt(0).toString(16);
    id = `\\3${firstChar.slice(-1)} ${id.slice(1)}`;
  }

  // Escape colons
  return id.replace(/:/g, "\\:");
}
