import { Component, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import type { StaffCalendarResponse } from "@adeni/shared";
import { AdeniFeedbackService, PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-staff-calendar",
  standalone: true,
  imports: [PortalPageComponent, RouterLink],
  templateUrl: "./staff-calendar.component.html",
  styleUrl: "./staff-calendar.component.scss",
})
export class StaffCalendarComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly calendar = signal<StaffCalendarResponse | null>(null);
  readonly weekStart = signal(startOfWeek(new Date()));

  staffId = "";

  ngOnInit(): void {
    this.staffId = this.route.snapshot.paramMap.get("id") ?? "";
    void this.load();
  }

  weekLabel(): string {
    const start = this.weekStart();
    const end = addDays(start, 6);
    return `${formatDay(start)} – ${formatDay(end)}`;
  }

  prevWeek(): void {
    this.weekStart.set(addDays(this.weekStart(), -7));
    void this.load();
  }

  nextWeek(): void {
    this.weekStart.set(addDays(this.weekStart(), 7));
    void this.load();
  }

  async load(): Promise<void> {
    if (!this.staffId) {
      this.error.set("Staff member not found.");
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    const from = this.weekStart();
    const to = addDays(from, 7);
    try {
      await this.feedback.runLoading(async () => {
        const data = await this.api.withAuthorizedClient((c) =>
          c.getTenantStaffCalendar(this.staffId, {
            from: from.toISOString(),
            to: to.toISOString(),
          }),
        );
        this.calendar.set(data);
      }, "Loading calendar…");
    } catch {
      this.error.set("Could not load calendar.");
      this.calendar.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  formatWhen(iso: string): string {
    return new Date(iso).toLocaleString(undefined, {
      weekday: "short",
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
    });
  }

  statusLabel(status: number): string {
    switch (status) {
      case 0:
        return "Pending";
      case 1:
        return "Confirmed";
      case 2:
        return "Rejected";
      case 3:
        return "Cancelled";
      default:
        return "Unknown";
    }
  }
}

function startOfWeek(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  d.setHours(0, 0, 0, 0);
  d.setDate(d.getDate() + diff);
  return d;
}

function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

function formatDay(date: Date): string {
  return date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
}
