/**
 * Converts an array of strings to a comma-separated string.
 * Returns undefined if the resulting string is empty.
 */
export function arrayToCsvOrUndefined(values: string[]): string | undefined {
  const csv = values.join(",");
  return csv.length > 0 ? csv : undefined;
}
