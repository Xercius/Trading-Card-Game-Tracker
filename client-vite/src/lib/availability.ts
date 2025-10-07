export function getAvailabilityDisplay(
  availability: number,
  availabilityWithProxies: number,
  includeProxies: boolean
) {
  const label = includeProxies ? "A+P" : "A";
  const value = includeProxies ? availabilityWithProxies : availability;
  return { label, value };
}

export function formatAvailability(value: number) {
  return value.toLocaleString();
}
