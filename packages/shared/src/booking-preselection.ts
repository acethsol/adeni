export type BookingPreselection = { service?: string; practitioner?: string };
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
/** Selectors never authorize a booking or override its server-owned values. */
export function parseBookingPreselection(params: URLSearchParams): BookingPreselection {
  const result: BookingPreselection = {};
  for (const [key,value] of params) {
    if ((key !== "service" && key !== "practitioner") || params.getAll(key).length !== 1 || !guid.test(value) || value === "00000000-0000-0000-0000-000000000000") throw new Error("Invalid booking preselection");
    result[key] = value;
  }
  return result;
}
export function bookingPreselectionQuery(selection: BookingPreselection): string {
  const params = new URLSearchParams();
  if (selection.service) params.set("service",selection.service);
  if (selection.practitioner) params.set("practitioner",selection.practitioner);
  parseBookingPreselection(params);
  return params.size ? `?${params}` : "";
}
