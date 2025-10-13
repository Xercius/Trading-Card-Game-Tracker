/**
 * Converts an array of strings to a comma-separated string.
 * Returns undefined if the resulting string is empty.
 */
export function arrayToCsvOrUndefined(values: string[]): string | undefined {
  if (!values.some(v => v.length > 0)) {
    return undefined;
  }
  const csv = values.join(",");
  return csv;
}
