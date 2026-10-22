import { DatePipe, NgTemplateOutlet } from "@angular/common";
import { Component, computed, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import {
  DAY_OF_WEEK_LABELS,
  PORTAL_PERMISSION_ROLES,
  STAFF_ROLE_LABELS,
  defaultPermissionRoleForFloorRole,
  defaultStaffRoleForCategories,
  staffRolesForCategories,
  type PortalPermissionRole,
  type ServiceOffering,
  type StaffLeave,
  type StaffMember,
  type StaffRoleKey,
  type WeeklyAvailabilityRule,
  DEFAULT_PAGE_SIZE,
  LIST_PAGE_SIZE_OPTIONS,
  canAddPractitionerAtCount,
  formatTeamUsageMetric,
  sliceListPage,
  type SubscriptionUsage,
  type TenantEntitlements,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniExportMenuComponent,
  AdeniFeedbackService,
  AdeniFilterSelectComponent,
  AdeniIconBtnComponent,
  AdeniListFilterRowComponent,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniRowActionBarComponent,
  AdeniTablePersonCellComponent,
  AdeniModalComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniWizardComponent,
  PortalPageComponent,
  readPersistedListViewMode,
  sortDataRows,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  type AdeniExportMenuItem,
  type AdeniFilterSelectOption,
  type AdeniListViewMode,
  type AdeniPillTone,
  type AdeniWizardStep,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

const AVATAR_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const AVATAR_MAX_BYTES = 5 * 1024 * 1024;

const DAYS_ORDER = [1, 2, 3, 4, 5, 6, 0];
const STAFF_VIEW_STORAGE_KEY = "portal.staff";

type StaffFormTab = "profile" | "services" | "hours" | "leave";
type StaffBookingFilter = "all" | "bookable" | "inactive";
type StaffPortalFilter = "all" | "has_portal" | "none";

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
  imports: [
    PortalPageComponent,
    FormsModule,
    RouterLink,
    AdeniWizardComponent,
    AdeniModalComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniListPaginationComponent,
    AdeniListFilterRowComponent,
    AdeniFilterSelectComponent,
    AdeniExportMenuComponent,
    AdeniTablePersonCellComponent,
    AdeniRowActionBarComponent,
    AdeniDataTableComponent,
    AdeniDataTableActionsDirective,
    AdeniDataTableCellDirective,
    AdeniIconBtnComponent,
    AdeniPillComponent,
    NgTemplateOutlet,
    DatePipe,
  ],
  templateUrl: "./staff.component.html",
  styleUrl: "./staff.component.scss",
})
export class StaffComponent implements OnInit, OnDestroy {
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly staff = signal<StaffMember[]>([]);
  readonly entitlements = signal<TenantEntitlements | null>(null);
  readonly subscriptionUsage = signal<SubscriptionUsage | null>(null);
  readonly rosterSearch = signal("");
  readonly bookingFilter = signal<StaffBookingFilter>("all");
  readonly portalFilter = signal<StaffPortalFilter>("all");
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;

  readonly bookingFilterOptions: AdeniFilterSelectOption[] = [
    { value: "all", label: "All booking states" },
    { value: "bookable", label: "Bookable" },
    { value: "inactive", label: "Inactive" },
  ];

  readonly portalFilterOptions: AdeniFilterSelectOption[] = [
    { value: "all", label: "All portal access" },
    { value: "has_portal", label: "Has portal access" },
    { value: "none", label: "No portal access" },
  ];

  readonly staffExportItems: AdeniExportMenuItem[] = [
    {
      format: "csv",
      label: "CSV (.csv)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
    {
      format: "xlsx",
      label: "Excel (.xlsx)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
    {
      format: "pdf",
      label: "PDF (.pdf)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
  ];
  readonly viewMode = signal<AdeniListViewMode>(
    readPersistedListViewMode(STAFF_VIEW_STORAGE_KEY, "cards"),
  );
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
      title: "Profile",
      lede: "Photo and name as customers will see them.",
    },
    {
      id: "services",
      label: "Services",
      title: "Services",
      lede: "Leave blank to allow every active service.",
    },
    {
      id: "hours",
      label: "Hours",
      title: "Hours",
      lede: "Skip to follow the business calendar.",
    },
  ];

  readonly availableRoles = computed(() =>
    staffRolesForCategories(this.categorySlugs(), this.preserveRoleKey()),
  );

  readonly practitionersActive = computed(() => {
    const usage = this.subscriptionUsage();
    if (usage) {
      return usage.practitionersActive;
    }
    return this.staff().filter((m) => m.isActive).length;
  });

  readonly canAddPractitioner = computed(() =>
    canAddPractitionerAtCount(this.entitlements(), this.practitionersActive()),
  );

  readonly teamUsageLabel = computed(() => {
    const usage = this.subscriptionUsage();
    if (!usage?.entitlements.maxPractitioners) {
      return null;
    }
    return `${formatTeamUsageMetric(usage)} on your plan`;
  });

  readonly sortColumnId = signal<string | null>(null);
  readonly sortDirection = signal<AdeniDataTableSortDirection>("asc");

  readonly filteredStaff = computed(() => {
    let list = this.staff();
    switch (this.bookingFilter()) {
      case "bookable":
        list = list.filter((m) => m.isActive);
        break;
      case "inactive":
        list = list.filter((m) => !m.isActive);
        break;
      default:
        break;
    }
    switch (this.portalFilter()) {
      case "has_portal":
        list = list.filter((m) => m.portalAccessStatus !== "none");
        break;
      case "none":
        list = list.filter((m) => m.portalAccessStatus === "none");
        break;
      default:
        break;
    }

    const query = this.rosterSearch().trim().toLowerCase();
    if (!query) {
      return list;
    }
    return list.filter((member) => {
      const haystack = [
        member.displayName,
        member.firstName,
        member.lastName,
        member.title ?? "",
        this.roleLabel(member.roleKey),
      ]
        .join(" ")
        .toLowerCase();
      return haystack.includes(query);
    });
  });

  readonly sortedFilteredStaff = computed(() =>
    sortDataRows(
      this.filteredStaff(),
      this.sortColumnId(),
      this.sortDirection(),
      this.staffTableColumns,
    ),
  );

  readonly filteredStaffCount = computed(() => this.filteredStaff().length);

  readonly paginatedStaff = computed(() =>
    sliceListPage(this.sortedFilteredStaff(), this.page(), this.pageSize()),
  );

  readonly staffTableColumns: AdeniDataTableColumn<StaffMember>[] = [
    {
      id: "name",
      header: "Team member",
      cell: (m) => m.displayName.trim(),
      sortValue: (m) => m.displayName.trim().toLowerCase(),
    },
    {
      id: "title",
      header: "Title",
      cell: (m) => m.title?.trim() || "—",
      sortValue: (m) => (m.title?.trim() || "").toLowerCase(),
    },
    {
      id: "status",
      header: "Booking",
      cell: () => "",
      sortValue: (m) => (m.isActive ? 1 : 0),
    },
    {
      id: "access",
      header: "Portal",
      cell: () => "",
      sortValue: (m) => m.portalAccessStatus,
    },
  ];

  readonly staffRowClass = (member: StaffMember): string | undefined =>
    member.isActive ? undefined : "inactive-row";

  readonly trackStaffRow = (member: StaffMember): string => member.id;

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

  readonly inviteTarget = signal<StaffMember | null>(null);
  readonly roleTarget = signal<StaffMember | null>(null);
  readonly inviteRoles = PORTAL_PERMISSION_ROLES.filter((r) => r !== "owner");
  inviteEmail = "";
  invitePermissionRole: Exclude<PortalPermissionRole, "owner"> = "practitioner";
  editPermissionRole: Exclude<PortalPermissionRole, "owner"> = "practitioner";

  /** Local object URL or existing remote avatar for the open form. */
  readonly avatarPreviewUrl = signal<string | null>(null);
  private avatarFile: File | null = null;
  private avatarObjectUrl: string | null = null;

  ngOnInit(): void {
    void this.load();
  }

  onRosterSearchChange(value: string): void {
    this.rosterSearch.set(value);
    this.page.set(1);
  }

  onBookingFilterChange(value: StaffBookingFilter): void {
    this.bookingFilter.set(value);
    this.page.set(1);
  }

  onPortalFilterChange(value: StaffPortalFilter): void {
    this.portalFilter.set(value);
    this.page.set(1);
  }

  goToStaffPage(next: number): void {
    this.page.set(next);
  }

  onStaffPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

  onStaffSortColumnChange(columnId: string | null): void {
    this.sortColumnId.set(columnId);
    this.page.set(1);
  }

  onStaffSortDirectionChange(direction: AdeniDataTableSortDirection): void {
    this.sortDirection.set(direction);
    this.page.set(1);
  }

  ngOnDestroy(): void {
    this.clearAvatarSelection();
  }

  draftInitials(): string {
    const first = this.draft.firstName.trim();
    const last = this.draft.lastName.trim();
    const a = first.charAt(0);
    const b = last.charAt(0);
    const value = `${a}${b}`.toUpperCase();
    return value || "?";
  }

  onAvatarSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    if (!AVATAR_TYPES.has(file.type)) {
      this.feedback.error("Use JPEG, PNG, or WebP.");
      input.value = "";
      return;
    }
    if (file.size > AVATAR_MAX_BYTES) {
      this.feedback.error("Photo must be 5 MB or smaller.");
      input.value = "";
      return;
    }

    this.clearAvatarObjectUrl();
    this.avatarFile = file;
    this.avatarObjectUrl = URL.createObjectURL(file);
    this.avatarPreviewUrl.set(this.avatarObjectUrl);
  }

  private clearAvatarObjectUrl(): void {
    if (this.avatarObjectUrl) {
      URL.revokeObjectURL(this.avatarObjectUrl);
      this.avatarObjectUrl = null;
    }
  }

  private clearAvatarSelection(): void {
    this.clearAvatarObjectUrl();
    this.avatarFile = null;
    this.avatarPreviewUrl.set(null);
  }

  private async uploadAvatarIfNeeded(staffMemberId: string): Promise<void> {
    const file = this.avatarFile;
    if (!file) {
      return;
    }

    await this.api.withAuthorizedClient(async (c) => {
      const slot = await c.createStaffAvatarUploadUrl(file.type, file.size);
      const upload = await fetch(slot.uploadUrl, {
        method: "PUT",
        headers: { "Content-Type": file.type },
        body: file,
      });
      if (!upload.ok) {
        throw new Error("upload failed");
      }
      await c.updateTenantStaffAvatar(staffMemberId, { avatarImageKey: slot.storageKey });
    });
  }

  accessLabel(status: StaffMember["portalAccessStatus"]): string {
    switch (status) {
      case "active":
        return "Active";
      case "invite_pending":
        return "Invite pending";
      default:
        return "No access";
    }
  }

  bookingPillTone(member: StaffMember): AdeniPillTone {
    return member.isActive ? "success" : "danger";
  }

  accessPillTone(status: StaffMember["portalAccessStatus"]): AdeniPillTone {
    switch (status) {
      case "active":
        return "accent";
      case "invite_pending":
        return "warning";
      default:
        return "neutral";
    }
  }

  personAvatarStyle(member: StaffMember): string {
    return this.roleTone(member.roleKey);
  }

  openInvite(member: StaffMember): void {
    this.inviteTarget.set(member);
    this.inviteEmail = "";
    this.invitePermissionRole = defaultPermissionRoleForFloorRole(member.roleKey) as Exclude<
      PortalPermissionRole,
      "owner"
    >;
  }

  closeInvite(): void {
    this.inviteTarget.set(null);
  }

  openChangeRole(member: StaffMember): void {
    if (!member.businessUserId || member.permissionRole === "owner") {
      return;
    }
    this.roleTarget.set(member);
    this.editPermissionRole = (member.permissionRole ??
      defaultPermissionRoleForFloorRole(member.roleKey)) as Exclude<PortalPermissionRole, "owner">;
  }

  closeChangeRole(): void {
    this.roleTarget.set(null);
  }

  async sendInvite(): Promise<void> {
    const member = this.inviteTarget();
    if (!member || !this.inviteEmail.trim()) {
      return;
    }

    this.busy.set(member.id);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (c) => {
          await c.inviteTenantStaff(member.id, {
            email: this.inviteEmail.trim(),
            permissionRole: this.invitePermissionRole,
          });
        });
      });
      this.feedback.success("Invite sent — check API logs for the accept link in Development.");
      this.closeInvite();
      await this.load();
    } catch {
      this.feedback.error("Could not send invite.");
    } finally {
      this.busy.set(null);
    }
  }

  async resendInvite(member: StaffMember): Promise<void> {
    if (!member.portalInviteId) {
      return;
    }
    this.busy.set(member.id);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (c) => {
          await c.resendTenantAccessInvite(member.portalInviteId!);
        });
      });
      this.feedback.success("Invite resent.");
      await this.load();
    } catch {
      this.feedback.error("Could not resend invite.");
    } finally {
      this.busy.set(null);
    }
  }

  async revokeAccess(member: StaffMember): Promise<void> {
    const pending = member.portalAccessStatus === "invite_pending" && member.portalInviteId;
    const active = member.portalAccessStatus === "active" && member.businessUserId;
    if (!pending && !active) {
      return;
    }

    const ok = await this.confirmDialog.confirm({
      title: pending ? "Revoke invite?" : "Revoke portal access?",
      message: pending
        ? `${member.displayName} will no longer be able to accept this invite.`
        : `${member.displayName} will lose portal login immediately. You can invite them again later.`,
      confirmLabel: "Revoke",
      danger: true,
    });
    if (!ok) {
      return;
    }

    this.busy.set(member.id);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (c) => {
          if (pending) {
            await c.revokeTenantAccessInvite(member.portalInviteId!);
          } else {
            await c.revokeTenantAccessUser(member.businessUserId!);
          }
        });
      });
      this.feedback.success(pending ? "Invite revoked." : "Portal access revoked.");
      await this.load();
    } catch {
      this.feedback.error(
        pending
          ? "Could not revoke invite."
          : "Could not revoke access. The last owner cannot be removed.",
      );
    } finally {
      this.busy.set(null);
    }
  }

  async saveRoleChange(): Promise<void> {
    const member = this.roleTarget();
    if (!member?.businessUserId) {
      return;
    }

    this.busy.set(member.id);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (c) => {
          await c.updateTenantAccessUser(member.businessUserId!, {
            permissionRole: this.editPermissionRole,
          });
        });
      });
      this.feedback.success("Permission role updated.");
      this.closeChangeRole();
      await this.load();
    } catch {
      this.feedback.error("Could not change role. The last owner cannot be demoted.");
    } finally {
      this.busy.set(null);
    }
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
        await this.fetchStaffPageData();
      }, "Loading staff", "Fetching roster and services");
    } catch {
      this.error.set("Could not load staff. Try again.");
    } finally {
      this.loading.set(false);
    }
  }

  private async refreshStaff(): Promise<void> {
    try {
      await this.api.withAuthorizedClient(async (c) => {
        this.staff.set(await c.listTenantStaff());
      });
    } catch {
      this.feedback.error("Could not refresh the roster.");
    }
  }

  private async fetchStaffPageData(): Promise<void> {
    await this.api.withAuthorizedClient(async (c) => {
      const [staff, catalog, profile, usage] = await Promise.all([
        c.listTenantStaff(),
        c.getTenantServiceCatalog(),
        c.getTenantProfile().catch(() => null),
        c.getTenantSubscriptionUsage().catch(() => null),
      ]);
      this.staff.set(staff);
      this.services.set(catalog.items.filter((s) => s.isActive !== false));
      this.entitlements.set(usage?.entitlements ?? profile?.entitlements ?? null);
      this.subscriptionUsage.set(usage);
      const slugs = [
        profile?.categorySlug,
        ...(profile?.additionalCategorySlugs ?? []),
      ].filter((s): s is string => !!s?.trim());
      this.categorySlugs.set(slugs);
    });
  }

  openCreate(): void {
    if (!this.canAddPractitioner()) {
      const message = "Your plan team limit is reached. Upgrade on Plan to add more members.";
      this.error.set(message);
      this.feedback.error(message);
      return;
    }
    this.editingId.set(null);
    this.preserveRoleKey.set(null);
    this.formTab.set("profile");
    this.draft = this.emptyDraft();
    this.hourRows = defaultWorkingRows();
    this.leaveItems.set([]);
    this.leaveStart = "";
    this.leaveEnd = "";
    this.leaveReason = "";
    this.clearAvatarSelection();
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
    this.clearAvatarSelection();
    this.avatarPreviewUrl.set(member.avatarImageUrl ?? null);
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
    this.clearAvatarSelection();
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
            await this.uploadAvatarIfNeeded(editingId);
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
            await this.uploadAvatarIfNeeded(created.id);
          }
        });
      }, editingId ? "Saving changes" : "Adding team member", "Please wait");

      this.cancelForm();
      this.feedback.success(
        editingId ? "Changes saved successfully." : "Team member added successfully.",
        editingId ? "Updated" : "Added",
      );
      await this.refreshStaff();
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
