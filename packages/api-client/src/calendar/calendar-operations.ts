import { calendarConnectionsSchema, calendarConflictsSchema, calendarSelectionsSchema, externalCalendarsSchema, calendarActionSchema, calendarAuthorizationSchema, bookingLinkSchema, bookingLinkDestinationSchema, bookingPreselectionQuery,
  type CalendarProvider, type CalendarSelection, type BookingPreselection } from "@adeni/shared/calendar";
import type { ApiClientContext } from "../core/api-transport";
export class CalendarOperations {
  constructor(private readonly client: ApiClientContext) {}
  private async read<T>(path: string, schema: { parse(v: unknown): T }, signal?: AbortSignal): Promise<T> {
    return schema.parse(await (await this.client.request(path, { signal, cache: "no-store" })).json());
  }
  private async write<T>(path: string, body: unknown, schema: { parse(v: unknown): T }, method = "POST", signal?: AbortSignal): Promise<T> {
    return schema.parse(await (await this.client.request(path, { method, signal, cache: "no-store", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) })).json());
  }
  connections(signal?: AbortSignal) { return this.read("/api/v1/tenant/calendar/connections", calendarConnectionsSchema, signal); }
  connect(staffMemberId: string, provider: CalendarProvider, writeAccess: boolean, signal?: AbortSignal) {
    return this.write("/api/v1/tenant/calendar/connections", { staffMemberId, provider, writeAccess }, calendarAuthorizationSchema, "POST", signal);
  }
  calendars(id: string, signal?: AbortSignal) { return this.read(`/api/v1/tenant/calendar/connections/${encodeURIComponent(id)}/calendars`, externalCalendarsSchema, signal); }
  select(id: string, calendars: CalendarSelection[], signal?: AbortSignal) { return this.write(`/api/v1/tenant/calendar/connections/${encodeURIComponent(id)}/selection`, { calendars: calendarSelectionsSchema.parse(calendars) }, calendarActionSchema, "PUT", signal); }
  disconnect(id: string, signal?: AbortSignal) { return this.write(`/api/v1/tenant/calendar/connections/${encodeURIComponent(id)}/disconnect`, {}, calendarActionSchema, "POST", signal); }
  sync(id: string, signal?: AbortSignal) { return this.write(`/api/v1/tenant/calendar/connections/${encodeURIComponent(id)}/sync`, {}, calendarActionSchema, "POST", signal); }
  conflicts(signal?: AbortSignal) { return this.read("/api/v1/tenant/calendar/conflicts", calendarConflictsSchema, signal); }
  acknowledge(id: string, signal?: AbortSignal) { return this.write(`/api/v1/tenant/calendar/conflicts/${encodeURIComponent(id)}/acknowledge`, {}, calendarActionSchema, "POST", signal); }
  publishLink(locationId: string, selection: BookingPreselection = {}, signal?: AbortSignal) { return this.write("/api/v1/tenant/booking-links", { locationId, serviceId: selection.service ?? null, practitionerId: selection.practitioner ?? null }, bookingLinkSchema, "POST", signal); }
  resolveLink(slug: string, selection: BookingPreselection = {}, signal?: AbortSignal) { return this.read(`/api/v1/public/booking-links/${encodeURIComponent(slug)}${bookingPreselectionQuery(selection)}`, bookingLinkDestinationSchema, signal); }
  bookingContext(slug: string, selection: BookingPreselection, signal?: AbortSignal) { return this.read(`/api/v1/public/booking-context/${encodeURIComponent(slug)}${bookingPreselectionQuery(selection)}`, bookingLinkDestinationSchema, signal); }
}
