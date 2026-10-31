import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { AdeniApiError } from "@adeni/api-client";
import type {
  RecordStaffAttendanceEventRequest,
  StaffAttendanceScheduleTodayResponse,
  StaffAttendanceTodayResponse,
  VerifiedPresenceSessionResponse,
} from "@adeni/shared";
import { AdeniStaffButtonComponent, PortalPageComponent, injectAdeniTranslate } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { BusinessSessionService } from "../../core/services/business-session.service";

type AttendanceAction = "clock_in" | "break_start" | "break_end" | "clock_out";

@Component({
  selector: "app-my-attendance",
  standalone: true,
  imports: [PortalPageComponent, AdeniStaffButtonComponent, DatePipe],
  templateUrl: "./my-attendance.component.html",
  styleUrl: "./my-attendance.component.scss",
})
export class MyAttendanceComponent {
  private readonly api = inject(BusinessApiService);
  private readonly session = inject(BusinessSessionService);
  protected readonly tr = injectAdeniTranslate();

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly today = signal<StaffAttendanceTodayResponse | null>(null);
  readonly scheduleToday = signal<StaffAttendanceScheduleTodayResponse | null>(null);
  readonly presence = signal<VerifiedPresenceSessionResponse | null>(null);

  readonly scheduledWindows = computed(
    () => this.scheduleToday()?.scheduledWindows ?? this.today()?.scheduledWindows ?? [],
  );

  readonly nextAction = computed((): AttendanceAction | null => {
    const state = this.today()?.workState;
    switch (state) {
      case "not_working":
        return "clock_in";
      case "working":
        return "break_start";
      case "on_break":
        return "break_end";
      default:
        return null;
    }
  });

  readonly canClockOut = computed(() => {
    const state = this.today()?.workState;
    return state === "working" || state === "on_break";
  });

  readonly showVerifyPresence = computed(() => {
    const state = this.today()?.workState;
    const onShift = state === "working" || state === "on_break";
    return onShift && !this.presence();
  });

  constructor() {
    void this.refresh();
  }

  async refresh(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.session.ensureLoaded();
      const data = await this.api.withAuthorizedClient(async (client) => {
        const [today, schedule, session] = await Promise.all([
          client.getMyAttendanceToday(),
          client.getMyScheduleToday().catch(() => null),
          client.getMyPresenceSession().catch(() => null),
        ]);
        this.presence.set(session);
        this.scheduleToday.set(schedule);
        return today;
      });
      this.today.set(data);
    } catch {
      this.error.set(this.tr("businessApp.attendance.loadFailed"));
    } finally {
      this.loading.set(false);
    }
  }

  async act(eventType: AttendanceAction): Promise<void> {
    const snapshot = this.today();
    const locationId = snapshot?.businessLocationId;
    if (!locationId) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      const body: RecordStaffAttendanceEventRequest = { eventType, businessLocationId: locationId };
      if (eventType === "clock_in" && snapshot?.attendanceGeofenceRequired) {
        body.locationFix = await this.captureLocationFix();
      }
      await this.api.withAuthorizedClient((client) => client.recordMyAttendanceEvent(body));
      await this.refresh();
    } catch (err) {
      if (err instanceof AdeniApiError && err.code === "staff.location_verification_failed") {
        this.error.set(this.tr("businessApp.attendance.geofenceFailed", { name: snapshot?.locationName ?? "" }));
      } else if (err instanceof AdeniApiError && err.code === "attendance.location_not_configured") {
        this.error.set(this.tr("businessApp.attendance.locationNotConfigured"));
      } else {
        this.error.set(this.tr("businessApp.attendance.actionFailed"));
      }
    } finally {
      this.saving.set(false);
    }
  }

  private captureLocationFix(): Promise<RecordStaffAttendanceEventRequest["locationFix"]> {
    return new Promise((resolve, reject) => {
      if (!navigator.geolocation) {
        reject(new Error("geolocation_unavailable"));
        return;
      }
      navigator.geolocation.getCurrentPosition(
        (pos) => {
          resolve({
            latitude: pos.coords.latitude,
            longitude: pos.coords.longitude,
            accuracyMeters: Math.min(100, Math.max(0, Math.round(pos.coords.accuracy))),
            capturedAt: new Date().toISOString(),
          });
        },
        () => reject(new Error("geolocation_denied")),
        { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 },
      );
    });
  }

  actionLabel(action: AttendanceAction): string {
    return this.tr(`businessApp.attendance.actions.${action}`);
  }

  async verifyPresence(): Promise<void> {
    const snapshot = this.today();
    const locationId = snapshot?.businessLocationId;
    if (!locationId) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      const body: { businessLocationId: string; locationFix?: RecordStaffAttendanceEventRequest["locationFix"] } = {
        businessLocationId: locationId,
      };
      if (snapshot?.attendanceGeofenceRequired) {
        body.locationFix = await this.captureLocationFix();
      }
      const session = await this.api.withAuthorizedClient((client) => client.verifyPresence(body));
      this.presence.set(session);
    } catch (err) {
      if (err instanceof AdeniApiError && err.code === "staff.location_verification_failed") {
        this.error.set(this.tr("businessApp.attendance.geofenceFailed", { name: snapshot?.locationName ?? "" }));
      } else {
        this.error.set(this.tr("businessApp.attendance.presenceVerifyFailed"));
      }
    } finally {
      this.saving.set(false);
    }
  }
}
