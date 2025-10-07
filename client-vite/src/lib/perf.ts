// Device performance helpers. Safe for SSR and tests.

export function coreCount(): number {
  if (typeof navigator === "undefined") return 4; // SSR/tests fallback
  const n = Number((navigator as Partial<Navigator>).hardwareConcurrency ?? 4);
  return Number.isFinite(n) && n > 0 ? n : 4;
}

export function isLowCoreDevice(threshold = 4): boolean {
  return coreCount() <= threshold;
}

export function pageSizeForDevice(low = 60, high = 96, threshold = 4): number {
  return isLowCoreDevice(threshold) ? low : high;
}

export function overscanForDevice(lowCore = 8, highCore = 6, threshold = 4): number {
  return isLowCoreDevice(threshold) ? lowCore : highCore;
}
