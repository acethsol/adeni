import { NgTemplateOutlet } from "@angular/common";
import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import {
  DAY_OF_WEEK_LABELS,
  STAFF_ROLE_LABELS,
  defaultStaffRoleForCategories,
  staffRolesForCategories,
  type ServiceOffering,
  type StaffLeave,
  type StaffMember,
  type StaffRoleKey,
  type WeeklyAvailabilityRule,
} from "@adeni/shared";
import {
  AdeniConfirmService,
  AdeniFeedbackService,
  AdeniWizardComponent,
  PortalPageComponent,
  type AdeniWizardStep,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

const DAYS_ORDER = [1, 2, 3, 4, 5, 6, 0];

type StaffFormTab = "profile" | "services" | "hours" | "leave";

/** Create wizard steps (Leave only after save). */
const CREATE_STEPS: StaffFormTab[] = ["profile", "services", "hours"];

type DayRow = {
  dayOfWeek: number;
  openTime: string;
  closeTime: string;
  closed: boolean;
};

type StaffDraft = {
  firstName: string;
  lastName: string;
  displayName: string;
  roleKey: StaffRoleKey;
  title: string;
  bio: string;
  sortOrder: string;
  isActive: boolean;
  serviceOfferingIds: string[];
};

function toInputTime(value: string): string {
  return value.slice(0, 5);
}

function toApiTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

/** Empty schedule = inherit business hours (every day off here). */
function inheritedHourRows(): DayRow[] {
  return DAYS_ORDER.map((dayOfWeek) => ({
    dayOfWeek,
    openTime: "09:00",
    closeTime: "17:00",
    closed: true,
  }));
}

/** Create default: Mon–Sat open, Sunday off — same shape as business hours. */
function defaultWorkingRows(): DayRow[] {
  return DAYS_ORDER.map((dayOfWeek) => ({
    dayOfWeek,
    openTime: "09:00",
    closeTime: "17:00",
    closed: dayOfWeek === 0,
  }));
}

function rulesToRows(rules: WeeklyAvailabilityRule[]): DayRow[] {
  if (rules.length === 0) {
    return inheritedHourRows();
  }
  return DAYS_ORDER.map((dayOfWeek) => {
    const rule = rules.find((r) => r.dayOfWeek === dayOfWeek);
    if (!rule) {
      return { dayOfWeek, openTime: "09:00", closeTime: "17:00", closed: true };
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
  selector: "app-staff",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, RouterLink, AdeniWizardComponent, NgTemplateOutlet],
  templateUrl: "./staff.component.html",
  styleUrl: "./staff.component.scss",
})
export class StaffComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly staff = signal<StaffMember[]>([]);
  readonly services = signal<ServiceOffering[]>([]);
  readonly busy = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly showForm = signal(false);
  readonly formTab = signal<StaffFormTab>("profile");
  readonly categorySlugs = signal<string[]>([]);
  /** Keep an existing role in the dropdown even if category filters would hide it. */
  readonly preserveRoleKey = signal<string | null>(null);
  readonly roleLabels = STAFF_ROLE_LABELS;
  readonly dayLabels = DAY_OF_WEEK_LABELS;
  readonly createWizardSteps: readonly AdeniWizardStep[] = [
    {
      id: "profile",
      label: "Profile",
      title: "Team member profile",
      lede: "The display name is shown to customers when they book.",
    },
    {
      id: "services",
      label: "Services",
      title: "Assignable services",
      lede: "Leave blank to allow every active service.",
    },
    {
      id: "hours",
      label: "Hours",
      title: "Working hours",
      lede: "Skip to follow the business calendar.",
    },
  ];

  readonly availableRoles = computed(() =>
    staffRolesForCategories(this.categorySlugs(), this.preserveRoleKey()),
  );

  /** Create = wizard; edit = free tabs. */
  readonly isCreateWizard = computed(() => this.showForm() && !this.editingId());

  readonly createStepIndex = computed(() => {
    const idx = CREATE_STEPS.indexOf(this.formTab());
    return idx >= 0 ? idx : 0;
  });

  draft: StaffDraft = this.emptyDraft();
  hourRows: DayRow[] = rulesToRows([]);
  leaveItems = signal<StaffLeave[]>([]);
  leaveStart = "";
  leaveEnd = "";
  leaveReason = "";

  ngOnInit(): void {
    void this.load();
  }

  emptyDraft(): StaffDraft {
    return {
      firstName: "",
      lastName: "",
      displayName: "",
      roleKey: defaultStaffRoleForCategories(this.categorySlugs()),
      title: "",
      bio: "",
      sortOrder: "0",
      isActive: true,
      serviceOfferingIds: [],
    };
  }

  roleLabel(key: string): string {
    return this.roleLabels[key as StaffRoleKey] ?? key;
  }

  /** Role (+ title); legal name only when it differs from the public display name. */
  memberMeta(member: StaffMember): string {
    const parts: string[] = [];
    if (member.title?.trim()) {
      parts.push(member.title.trim());
    }
    const legal = `${member.firstName} ${member.lastName}`.trim();
    if (
      legal &&
      legal.toLowerCase() !== member.displayName.trim().toLowerCase()
    ) {
      parts.push(legal);
    }
    return parts.join(" · ");
  }

  initials(member: StaffMember): string {
    const fromParts = `${member.firstName?.[0] ?? ""}${member.lastName?.[0] ?? ""}`.trim();
    if (fromParts.length >= 1) {
      return fromParts.toUpperCase();
    }
    const words = member.displayName.trim().split(/\s+/).filter(Boolean);
    if (words.length >= 2) {
      return `${words[0]![0]}${words[1]![0]}`.toUpperCase();
    }
    return (member.displayName.trim().slice(0, 2) || "?").toUpperCase();
  }

  roleTone(roleKey: string): string {
    switch (roleKey) {
      case "stylist":
      case "barber":
        return "hair";
      case "nail_tech":
        return "nails";
      case "esthetician":
      case "therapist":
        return "spa";
      case "instructor":
      case "trainer":
        return "fit";
      case "manager":
      case "supervisor":
        return "lead";
      case "accountant":
      case "inventory_manager":
      case "hr":
      case "marketing":
      case "admin_staff":
        return "ops";
      default:
        return "neutral";
    }
  }

  servicePreview(member: StaffMember): string[] {
    const names = member.serviceOfferingIds.map((id) => this.serviceName(id));
    return names.slice(0, 3);
  }

  serviceOverflow(member: StaffMember): number {
    return Math.max(0, member.serviceOfferingIds.length - 3);
  }

  setFormTab(tab: StaffFormTab): void {
    if (this.isCreateWizard()) {
      return;
    }
    if (tab === "leave" && !this.editingId()) {
      return;
    }
    this.formTab.set(tab);
  }

  private profileReady(): boolean {
    const first = this.draft.firstName.trim();
    const last = this.draft.lastName.trim();
    if (!first || !last) {
      this.feedback.error("First and last name are required.");
      return false;
    }
    const display =
      this.draft.displayName.trim() || `${first} ${last}`.trim();
    if (display.length < 2) {
      this.feedback.error("Display name needs at least 2 characters.");
      return false;
    }
    return true;
  }

  wizardBack(): void {
    const idx = this.createStepIndex();
    if (idx <= 0) {
      return;
    }
    this.formTab.set(CREATE_STEPS[idx - 1]!);
  }

  wizardNext(): void {
    const idx = this.createStepIndex();
    const current = CREATE_STEPS[idx];
    if (current === "profile" && !this.profileReady()) {
      return;
    }
    if (idx >= CREATE_STEPS.length - 1) {
      return;
    }
    this.formTab.set(CREATE_STEPS[idx + 1]!);
  }

  wizardSkip(): void {
    const idx = this.createStepIndex();
    if (idx <= 0 || idx >= CREATE_STEPS.length - 1) {
      return;
    }
    this.formTab.set(CREATE_STEPS[idx + 1]!);
  }

  wizardGo(id: string): void {
    const idx = CREATE_STEPS.indexOf(id as StaffFormTab);
    if (idx < 0 || idx >= this.createStepIndex()) {
      return;
    }
    this.formTab.set(CREATE_STEPS[idx]!);
  }

  isLastCreateStep(): boolean {
    return this.createStepIndex() >= CREATE_STEPS.length - 1;
  }

  serviceName(id: string): string {
    return this.services().find((s) => s.id === id)?.name ?? "Service";
  }

  serviceNames(ids: string[]): string {
    return ids.map((id) => this.serviceName(id)).join(" · ");
  }

  syncDisplayName(): void {
    if (this.editingId()) {
      return;
    }
    const first = this.draft.firstName.trim();
    const last = this.draft.lastName.trim();
    if (first || last) {
      this.draft.displayName = `${first} ${last}`.trim();
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (c) => {
          const [staff, catalog, profile] = await Promise.all([
            c.listTenantStaff(),
            c.getTenantServiceCatalog(),
            c.getTenantProfile().catch(() => null),
          ]);
          this.staff.set(staff);
          this.services.set(catalog.items.filter((s) => s.isActive !== false));
          const slugs = [
            profile?.categorySlug,
            ...(profile?.additionalCategorySlugs ?? []),
          ].filter((s): s is string => !!s?.trim());
          this.categorySlugs.set(slugs);
        });
      }, "Loading team…");
    } catch {
      this.error.set("Could not load staff. Try again.");
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
    this.editingId.set(null);
    this.preserveRoleKey.set(null);
    this.formTab.set("profile");
    this.draft = this.emptyDraft();
    this.hourRows = defaultWorkingRows();
    this.leaveItems.set([]);
    this.leaveStart = "";
    this.leaveEnd = "";
    this.leaveReason = "";
    this.showForm.set(true);
  }

  async openEdit(member: StaffMember): Promise<void> {
    this.editingId.set(member.id);
    this.preserveRoleKey.set(member.roleKey);
    this.formTab.set("profile");
    this.draft = {
      firstName: member.firstName,
      lastName: member.lastName,
      displayName: member.displayName,
      roleKey: member.roleKey,
      title: member.title ?? "",
      bio: member.bio ?? "",
      sortOrder: String(member.sortOrder ?? 0),
      isActive: member.isActive,
      serviceOfferingIds: [...member.serviceOfferingIds],
    };
    this.showForm.set(true);
    try {
      await this.api.withAuthorizedClient(async (c) => {
        const [hours, leave] = await Promise.all([
          c.getTenantStaffHours(member.id),
          c.listTenantStaffLeave(member.id),
        ]);
        this.hourRows = rulesToRows(hours);
        this.leaveItems.set(leave);
      });
    } catch {
      this.hourRows = rulesToRows([]);
      this.leaveItems.set([]);
    }
  }

  async copyBusinessHours(): Promise<void> {
    try {
      const rules = await this.api.withAuthorizedClient((c) => c.getTenantAvailability());
      this.hourRows = rulesToRows(rules);
      this.feedback.success("Copied business hours. Save to apply.");
    } catch {
      this.feedback.error("Could not load business hours.");
    }
  }

  clearStaffHours(): void {
    this.hourRows = inheritedHourRows();
  }

  setDayOn(row: DayRow, on: boolean): void {
    row.closed = !on;
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
    this.preserveRoleKey.set(null);
    this.formTab.set("profile");
    this.draft = this.emptyDraft();
    this.leaveItems.set([]);
  }

  toggleService(serviceId: string): void {
    const current = this.draft.serviceOfferingIds;
    if (current.includes(serviceId)) {
      this.draft.serviceOfferingIds = current.filter((id) => id !== serviceId);
    } else {
      this.draft.serviceOfferingIds = [...current, serviceId];
    }
  }

  async save(): Promise<void> {
    if (!this.profileReady()) {
      this.formTab.set("profile");
      return;
    }

    const firstName = this.draft.firstName.trim();
    const lastName = this.draft.lastName.trim();
    const displayName =
      this.draft.displayName.trim() || `${firstName} ${lastName}`.trim();

    const sortOrder = Number.parseInt(this.draft.sortOrder, 10) || 0;
    const editingId = this.editingId();
    this.busy.set(editingId ?? "create");

    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (c) => {
          if (editingId) {
            await c.updateTenantStaff(editingId, {
              firstName,
              lastName,
              displayName,
              roleKey: this.draft.roleKey,
              title: this.draft.title.trim() || null,
              bio: this.draft.bio.trim() || null,
              sortOrder,
              isActive: this.draft.isActive,
            });
            await c.replaceTenantStaffServices(editingId, {
              serviceOfferingIds: this.draft.serviceOfferingIds,
            });
            await c.replaceTenantStaffHours(editingId, rowsToRules(this.hourRows));
          } else {
            const created = await c.createTenantStaff({
              firstName,
              lastName,
              displayName,
              roleKey: this.draft.roleKey,
              title: this.draft.title.trim() || null,
              bio: this.draft.bio.trim() || null,
              sortOrder,
              serviceOfferingIds: this.draft.serviceOfferingIds,
            });
            const hours = rowsToRules(this.hourRows);
            if (hours.length > 0) {
              await c.replaceTenantStaffHours(created.id, hours);
            }
          }
        });
      }, editingId ? "Saving…" : "Adding…");

      this.feedback.success(editingId ? "Team member updated." : "Team member added.");
      this.cancelForm();
      await this.load();
    } catch {
      this.feedback.error("Could not save team member.");
    } finally {
      this.busy.set(null);
    }
  }

  async addLeave(): Promise<void> {
    const editingId = this.editingId();
    if (!editingId || !this.leaveStart || !this.leaveEnd) {
      this.feedback.error("Choose leave start and end.");
      return;
    }

    try {
      const created = await this.api.withAuthorizedClient((c) =>
        c.createTenantStaffLeave(editingId, {
          startAt: new Date(this.leaveStart).toISOString(),
          endAt: new Date(this.leaveEnd).toISOString(),
          reason: this.leaveReason.trim() || null,
        }),
      );
      this.leaveItems.update((items) =>
        [...items, created].sort((a, b) => +new Date(a.startAt) - +new Date(b.startAt)),
      );
      this.leaveStart = "";
      this.leaveEnd = "";
      this.leaveReason = "";
      if (created.conflictingBookingIds.length > 0) {
        this.feedback.success(
          `Leave saved. Warning: ${created.conflictingBookingIds.length} existing booking(s) overlap.`,
        );
      } else {
        this.feedback.success("Leave saved.");
      }
    } catch {
      this.feedback.error("Could not save leave.");
    }
  }

  async removeLeave(leave: StaffLeave): Promise<void> {
    const editingId = this.editingId();
    if (!editingId) {
      return;
    }
    const ok = await this.confirmDialog.confirm({
      title: "Remove leave",
      message: "Delete this leave block?",
      confirmLabel: "Remove",
      danger: true,
    });
    if (!ok) {
      return;
    }
    try {
      await this.api.withAuthorizedClient((c) =>
        c.deleteTenantStaffLeave(editingId, leave.id),
      );
      this.leaveItems.update((items) => items.filter((x) => x.id !== leave.id));
      this.feedback.success("Leave removed.");
    } catch {
      this.feedback.error("Could not remove leave.");
    }
  }

  async deactivate(member: StaffMember): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: "Deactivate team member",
      message: `Hide ${member.displayName} from online booking? Existing bookings stay assigned.`,
      confirmLabel: "Deactivate",
      danger: true,
    });
    if (!ok) {
      return;
    }

    this.busy.set(member.id);
    try {
      await this.api.withAuthorizedClient((c) => c.deactivateTenantStaff(member.id));
      this.feedback.success(`${member.displayName} is no longer bookable online.`);
      await this.load();
    } catch {
      this.feedback.error("Could not deactivate.");
    } finally {
      this.busy.set(null);
    }
  }

  formatWhen(iso: string): string {
    return new Date(iso).toLocaleString(undefined, {
      dateStyle: "medium",
      timeStyle: "short",
    });
  }
}
