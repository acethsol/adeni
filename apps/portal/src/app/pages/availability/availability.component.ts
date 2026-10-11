import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { DAY_OF_WEEK_LABELS, type WeeklyAvailabilityRule } from "@adeni/shared";
import { AdeniCarbonIconComponent, PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

const DAYS_ORDER = [1, 2, 3, 4, 5, 6, 0];

type DayRow = {
  dayOfWeek: number;
  openTime: string;
  closeTime: string;
  closed: boolean;
};

function toInputTime(value: string): string {
  return value.slice(0, 5);
}

function toApiTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

function rulesToRows(rules: WeeklyAvailabilityRule[]): DayRow[] {
  return DAYS_ORDER.map((dayOfWeek) => {
    const rule = rules.find((r) => r.dayOfWeek === dayOfWeek);
    if (!rule) {
      return { dayOfWeek, openTime: "09:00", closeTime: "17:00", closed: dayOfWeek === 0 };
    }
    return {
      dayOfWeek,
      openTime: toInputTime(rule.openTime),
      closeTime: toInputTime(rule.closeTime),
      closed: false,
    };
  });
}

function rowsToRules(rows: DayRow[]): WeeklyAvailabilityRule[] {
  return rows
    .filter((row) => !row.closed)
    .map((row) => ({
      dayOfWeek: row.dayOfWeek,
      openTime: toApiTime(row.openTime),
      closeTime: toApiTime(row.closeTime),
    }));
}

@Component({
  selector: "app-availability",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, AdeniCarbonIconComponent],
  templateUrl: "./availability.component.html",
  styleUrl: "./availability.component.scss",
})
export class AvailabilityComponent implements OnInit {
  private readonly api = inject(BusinessApiService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly dayLabels = DAY_OF_WEEK_LABELS;

  rows: DayRow[] = [];

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const rules = await this.api.withAuthorizedClient((c) => c.getTenantAvailability());
      this.rows = rulesToRows(rules);
    } catch {
      this.error.set("Could not load availability.");
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    try {
      const saved = await this.api.withAuthorizedClient((c) =>
        c.replaceTenantAvailability(rowsToRules(this.rows)),
      );
      this.rows = rulesToRows(saved);
    } catch {
      this.error.set("Could not save availability.");
    } finally {
      this.saving.set(false);
    }
  }
}
