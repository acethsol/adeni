import { Component, DestroyRef, OnInit, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { PortalPageComponent, AdeniLocaleService, injectAdeniApiError } from "@adeni/ui";
import { calendarUiText } from "@adeni/shared/calendar-copy";
import type { CalendarConnection, CalendarConflict, CalendarProvider, ExternalCalendar, StaffMember } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({ selector: "app-calendar-sync", standalone: true, imports: [FormsModule, RouterLink, PortalPageComponent],
  templateUrl: "./calendar-sync.component.html" })
export class CalendarSyncComponent implements OnInit {
  private readonly locale = inject(AdeniLocaleService);
  readonly tr = (key: string) => calendarUiText(this.locale.locale(), key.replace(/^calendarUi\./, "")); private readonly apiErr = injectAdeniApiError(); private readonly api = inject(BusinessApiService);
  readonly connections = signal<CalendarConnection[]>([]); readonly conflicts = signal<CalendarConflict[]>([]); readonly staff = signal<StaffMember[]>([]);
  readonly calendars = signal<ExternalCalendar[]>([]); readonly selected = signal<string[]>([]); readonly editing = signal<CalendarConnection | null>(null);
  readonly busy = signal(false); readonly error = signal<string | null>(null); readonly notice = signal("");
  staffId = ""; provider: CalendarProvider = "google"; writeAccess = false; outputId = "";
  private abort = new AbortController(); private alive = true;
  constructor() { inject(DestroyRef).onDestroy(() => { this.alive = false; this.abort.abort(); this.calendars.set([]); }); }
  async ngOnInit(): Promise<void> { await this.run(async () => { const rows = await this.api.withAuthorizedClient(c => c.listTenantStaff()); if (!this.alive) return; this.staff.set(rows); await this.refresh(); }); }
  private async run(fn: () => Promise<void>): Promise<void> { if (this.busy()) return; this.busy.set(true); this.error.set(null); try { await fn(); } catch (e) { if (this.alive) this.error.set(this.apiErr(e, "calendarUi.error")); } finally { if (this.alive) this.busy.set(false); } }
  private async refresh(): Promise<void> {
    const data = await this.api.withAuthorizedClient(async c => ({ connections: await c.calendar.connections(this.abort.signal), conflicts: await c.calendar.conflicts(this.abort.signal) }));
    if (this.alive) { this.connections.set(data.connections); this.conflicts.set(data.conflicts); }
  }
  async reload(): Promise<void> { await this.run(() => this.refresh()); }
  async connect(): Promise<void> { await this.run(async () => {
    const result = await this.api.withAuthorizedClient(c => c.calendar.connect(this.staffId, this.provider, this.writeAccess, this.abort.signal));
    if (!this.alive) return;
    const url = new URL(result.launchUrl);
    if (url.protocol !== "https:" || url.pathname !== "/api/v1/calendar/oauth/launch") throw new Error("Invalid calendar authorization destination");
    window.location.assign(url.href);
  }); }
  async edit(connection: CalendarConnection): Promise<void> { await this.run(async () => {
    const choices = await this.api.withAuthorizedClient(c => c.calendar.calendars(connection.id, this.abort.signal));
    if (!this.alive) return; this.editing.set(connection); this.calendars.set(choices);
    this.selected.set(connection.selections.filter(x => !x.isWriteTarget).map(x => x.id)); this.outputId = connection.selections.find(x => x.isWriteTarget)?.id ?? "";
  }); }
  toggle(id: string): void { this.selected.update(current => current.includes(id) ? current.filter(x => x !== id) : [...current, id]); }
  async save(): Promise<void> { const connection = this.editing(); if (!connection) return; await this.run(async () => {
    const choices = this.calendars(); const selections = this.selected().map(id => ({ id, timeZoneId: choices.find(x => x.id === id)?.timeZoneId ?? "UTC", isWriteTarget: false }));
    if (this.outputId) selections.push({ id: this.outputId, timeZoneId: choices.find(x => x.id === this.outputId)?.timeZoneId ?? "UTC", isWriteTarget: true });
    await this.api.withAuthorizedClient(c => c.calendar.select(connection.id, selections, this.abort.signal));
    if (this.alive) this.editing.set(null); await this.refresh();
  }); }
  async sync(connection: CalendarConnection): Promise<void> { await this.run(async () => { await this.api.withAuthorizedClient(c => c.calendar.sync(connection.id, this.abort.signal)); await this.refresh(); if (this.alive) this.notice.set(this.tr("calendarUi.syncQueued")); }); }
  async disconnect(connection: CalendarConnection): Promise<void> {
    if (!window.confirm(this.tr("calendarUi.disconnectWarning"))) return;
    await this.run(async () => { await this.api.withAuthorizedClient(c => c.calendar.disconnect(connection.id, this.abort.signal)); if (this.alive) this.editing.set(null); await this.refresh(); });
  }
  async acknowledge(conflict: CalendarConflict): Promise<void> { await this.run(async () => { await this.api.withAuthorizedClient(c => c.calendar.acknowledge(conflict.id, this.abort.signal)); await this.refresh(); }); }
  name(id: string): string { return this.staff().find(x => x.id === id)?.displayName ?? this.tr("calendarUi.practitioner"); }
}
