import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import {
  REPORT_KEYS,
  buildReportExportMenuItems,
  hasTenantEntitlement,
  mergeTenantEntitlements,
  type ManagerStaffAttendanceOverrideRequest,
  type ReportExportFormat,
  type StaffAttendanceHistoryResponse,
  type StaffAttendanceTeamMember,
  type StaffAttendanceWorkState,
  type TenantEntitlements,
} from "@adeni/shared";
import {
  AdeniExportMenuComponent,
  AdeniFeedbackService,
  AdeniModalComponent,
  AdeniStaffButtonComponent,
  AdeniTabsComponent,
  PortalPageComponent,
  injectAdeniTranslate,
  type AdeniExportMenuItem,
  type AdeniTab,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { BusinessActiveLocationService } from "../../core/services/business-active-location.service";
import { BusinessSessionService } from "../../core/services/business-session.service";

type AttendanceAction = "clock_in" | "break_start" | "break_end" | "clock_out";

@Component({
  selector: "app-team-attendance",
  standalone: true,
  imports: [
    PortalPageComponent,
    AdeniStaffButtonComponent,
    AdeniModalComponent,
    AdeniExportMenuComponent,
    AdeniTabsComponent,
    FormsModule,
    DatePipe,
  ],
  templateUrl: "./team-attendance.component.html",
  styleUrl: "./team-attendance.component.scss",
})
export class TeamAttendanceComponent {
  private readonly api = inject(BusinessApiService);
  private readonly session = inject(BusinessSessionService);
  private readonly activeLocation = inject(BusinessActiveLocationService);
  private readonly feedback = inject(AdeniFeedbackService);
  protected readonly tr = injectAdeniTranslate();

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly exporting = signal(false);
  readonly entitlements = signal<TenantEntitlements | null>(null);
  readonly error = signal<string | null>(null);
  readonly team = signal<StaffAttendanceTeamMember[]>([]);
  readonly overrideMember = signal<StaffAttendanceTeamMember | null>(null);
  readonly overrideEventType = signal<AttendanceAction>("clock_in");
  readonly overrideReason = signal("");
  readonly managerView = signal<"today" | "timesheets">("today");
  readonly timesheetStaffId = signal<string>("");
  readonly historyFrom = signal(this.isoDateDaysAgo(6));
  readonly historyTo = signal(this.isoDateDaysAgo(0));
  readonly historyLoading = signal(false);
  readonly history = signal<StaffAttendanceHistoryResponse | null>(null);
  readonly historyMember = signal<StaffAttendanceTeamMember | null>(null);
  readonly correctionEventId = signal("");
  readonly correctionReason = signal("");

  readonly managerTabs = computed((): AdeniTab[] => [
    { id: "today", label: this.tr("businessApp.attendance.teamTabToday") },
    { id: "timesheets", label: this.tr("businessApp.attendance.teamTabTimesheets") },
  ]);

  readonly workingCount = computed(
    () => this.team().filter((m) => m.workState === "working").length,
  );
  readonly onBreakCount = computed(
    () => this.team().filter((m) => m.workState === "on_break").length,
  );
  readonly canExportRegister = computed(() =>
    hasTenantEntitlement(this.entitlements(), "attendanceReporting"),
  );
  readonly exportMenuItems = computed((): AdeniExportMenuItem[] =>
    buildReportExportMenuItems({
      entitlements: this.entitlements(),
      csv: true,
      xlsx: false,
      pdf: false,
    }),
  );

  readonly overrideActions = computed((): AttendanceAction[] => {
    const state = this.overrideMember()?.workState;
    switch (state) {
      case "not_working":
        return ["clock_in"];
      case "working":
        return ["break_start", "clock_out"];
      case "on_break":
        return ["break_end", "clock_out"];
      default:
        return ["clock_in"];
    }
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
        const profile = await client.getTenantProfile().catch(() => null);
        const planEntitlements = await client.getTenantEntitlements().catch(() => null);
        const merged = mergeTenantEntitlements(
          planEntitlements,
          profile?.entitlements,
          profile?.entitlements,
        );
        this.entitlements.set(merged);
        const locationId =
          this.activeLocation.tenantApiLocationId(merged) ??
          this.activeLocation.resolveContextLocation(profile)?.id;
        return client.getTeamAttendance(locationId ? { locationId } : undefined);
      });
      this.team.set(data.members);
      if (!this.timesheetStaffId() && data.members.length > 0) {
        this.timesheetStaffId.set(data.members[0]!.staffMemberId);
      }
    } catch {
      this.error.set(this.tr("businessApp.attendance.teamLoadFailed"));
    } finally {
      this.loading.set(false);
    }
  }

  onManagerTabChange(tabId: string): void {
    this.managerView.set(tabId === "timesheets" ? "timesheets" : "today");
    if (tabId === "timesheets") {
      void this.loadHistory();
    }
  }

  openTimesheet(member: StaffAttendanceTeamMember): void {
    this.timesheetStaffId.set(member.staffMemberId);
    this.managerView.set("timesheets");
    void this.loadHistory();
  }

  async loadHistory(): Promise<void> {
    const staffId = this.timesheetStaffId();
    if (!staffId) {
      return;
    }
    this.historyLoading.set(true);
    this.error.set(null);
    try {
      const data = await this.api.withAuthorizedClient((client) =>
        client.getStaffAttendanceHistory(staffId, {
          from: this.historyFrom(),
          to: this.historyTo(),
        }),
      );
      this.history.set(data);
    } catch {
      this.error.set(this.tr("businessApp.attendance.historyLoadFailed"));
      this.history.set(null);
    } finally {
      this.historyLoading.set(false);
    }
  }

  openHistoryDetail(member: StaffAttendanceTeamMember): void {
    this.historyMember.set(member);
    this.correctionEventId.set("");
    this.correctionReason.set("");
    void this.loadHistoryForModal(member.staffMemberId);
  }

  closeHistoryDetail(): void {
    this.historyMember.set(null);
  }

  private async loadHistoryForModal(staffMemberId: string): Promise<void> {
    this.historyLoading.set(true);
    try {
      const data = await this.api.withAuthorizedClient((client) =>
        client.getStaffAttendanceHistory(staffMemberId, {
          from: this.historyFrom(),
          to: this.historyTo(),
        }),
      );
      this.history.set(data);
    } catch {
      this.feedback.error(this.tr("businessApp.attendance.historyLoadFailed"));
    } finally {
      this.historyLoading.set(false);
    }
  }

  async submitCorrection(): Promise<void> {
    const member = this.historyMember();
    const relatedEventId = this.correctionEventId().trim();
    const reason = this.correctionReason().trim();
    if (!member || !relatedEventId || reason.length < 10) {
      this.error.set(this.tr("businessApp.attendance.correctionReasonTooShort"));
      return;
    }
    this.saving.set(true);
    try {
      await this.api.withAuthorizedClient((client) =>
        client.appendStaffAttendanceCorrection(member.staffMemberId, {
          relatedEventId,
          reason,
        }),
      );
      this.correctionReason.set("");
      this.feedback.success(this.tr("businessApp.attendance.correctionSaved"));
      await this.loadHistoryForModal(member.staffMemberId);
    } catch {
      this.feedback.error(this.tr("businessApp.attendance.correctionFailed"));
    } finally {
      this.saving.set(false);
    }
  }

  openOverride(member: StaffAttendanceTeamMember): void {
    this.overrideMember.set(member);
    const actions = this.actionsForState(member.workState);
    this.overrideEventType.set(actions[0] ?? "clock_in");
    this.overrideReason.set("");
  }

  closeOverride(): void {
    this.overrideMember.set(null);
  }

  async submitOverride(): Promise<void> {
    const member = this.overrideMember();
    const locationId = await this.api.withAuthorizedClient(async (client) => {
      const profile = await client.getTenantProfile().catch(() => null);
      return (
        this.activeLocation.tenantApiLocationId(this.entitlements()) ??
        this.activeLocation.resolveContextLocation(profile)?.id
      );
    });
    const reason = this.overrideReason().trim();
    if (!member || !locationId || reason.length < 10) {
      this.error.set(this.tr("businessApp.attendance.overrideReasonTooShort"));
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      const body: ManagerStaffAttendanceOverrideRequest = {
        eventType: this.overrideEventType(),
        businessLocationId: locationId,
        reason,
      };
      await this.api.withAuthorizedClient((client) =>
        client.recordManagerAttendanceOverride(member.staffMemberId, body),
      );
      this.closeOverride();
      await this.refresh();
    } catch {
      this.error.set(this.tr("businessApp.attendance.overrideFailed"));
    } finally {
      this.saving.set(false);
    }
  }

  workStateLabel(state: StaffAttendanceWorkState): string {
    return this.tr(`businessApp.attendance.workState.${state}`);
  }

  actionLabel(action: AttendanceAction): string {
    return this.tr(`businessApp.attendance.actions.${action}`);
  }

  async downloadAttendanceRegister(format: ReportExportFormat): Promise<void> {
    if (format !== "csv" || !this.canExportRegister()) {
      return;
    }
    this.exporting.set(true);
    try {
      const to = new Date();
      const from = new Date(to);
      from.setDate(from.getDate() - 30);
      const blob = await this.api.withAuthorizedClient((client) =>
        client.downloadTenantReportExport({
          reportKey: REPORT_KEYS.attendanceRegister,
          from: from.toISOString(),
          to: to.toISOString(),
          format: "csv",
          locationId: this.activeLocation.reportExportLocationId(this.entitlements()),
        }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `attendance-register-${to.toISOString().slice(0, 10)}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
      this.feedback.success(this.tr("businessApp.attendance.attendanceRegisterDownloaded"));
    } catch {
      this.feedback.error(this.tr("businessApp.attendance.couldNotDownloadAttendanceRegister"));
    } finally {
      this.exporting.set(false);
    }
  }

  private isoDateDaysAgo(days: number): string {
    const d = new Date();
    d.setDate(d.getDate() - days);
    return d.toISOString().slice(0, 10);
  }

  private actionsForState(state: StaffAttendanceWorkState): AttendanceAction[] {
    switch (state) {
      case "not_working":
        return ["clock_in"];
      case "working":
        return ["break_start", "clock_out"];
      case "on_break":
        return ["break_end", "clock_out"];
      default:
        return ["clock_in"];
    }
  }
}
