import type { ApiClientContext } from "../core/api-transport";
import type { CalendarOperations } from "./calendar-operations";
/** Keep optional integration schemas and transport operations out of startup bundles. */
export class CalendarApi {
  private loaded?: Promise<CalendarOperations>;
  constructor(private readonly client: ApiClientContext) {}
  private operations(): Promise<CalendarOperations> {
    return this.loaded ??= import("./calendar-operations").then(module => new module.CalendarOperations(this.client));
  }
  connections(...args: Parameters<CalendarOperations["connections"]>): ReturnType<CalendarOperations["connections"]> { return this.operations().then(api => api.connections(...args)); }
  connect(...args: Parameters<CalendarOperations["connect"]>): ReturnType<CalendarOperations["connect"]> { return this.operations().then(api => api.connect(...args)); }
  calendars(...args: Parameters<CalendarOperations["calendars"]>): ReturnType<CalendarOperations["calendars"]> { return this.operations().then(api => api.calendars(...args)); }
  select(...args: Parameters<CalendarOperations["select"]>): ReturnType<CalendarOperations["select"]> { return this.operations().then(api => api.select(...args)); }
  disconnect(...args: Parameters<CalendarOperations["disconnect"]>): ReturnType<CalendarOperations["disconnect"]> { return this.operations().then(api => api.disconnect(...args)); }
  sync(...args: Parameters<CalendarOperations["sync"]>): ReturnType<CalendarOperations["sync"]> { return this.operations().then(api => api.sync(...args)); }
  conflicts(...args: Parameters<CalendarOperations["conflicts"]>): ReturnType<CalendarOperations["conflicts"]> { return this.operations().then(api => api.conflicts(...args)); }
  acknowledge(...args: Parameters<CalendarOperations["acknowledge"]>): ReturnType<CalendarOperations["acknowledge"]> { return this.operations().then(api => api.acknowledge(...args)); }
  publishLink(...args: Parameters<CalendarOperations["publishLink"]>): ReturnType<CalendarOperations["publishLink"]> { return this.operations().then(api => api.publishLink(...args)); }
  resolveLink(...args: Parameters<CalendarOperations["resolveLink"]>): ReturnType<CalendarOperations["resolveLink"]> { return this.operations().then(api => api.resolveLink(...args)); }
  bookingContext(...args: Parameters<CalendarOperations["bookingContext"]>): ReturnType<CalendarOperations["bookingContext"]> { return this.operations().then(api => api.bookingContext(...args)); }
}
