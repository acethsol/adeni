import { Component, computed, inject, input, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { AdeniApiClient } from "@adeni/api-client";
import {
  formatPrice,
  formatSlotTime,
  type BusinessLocation,
  type CustomerMembership,
  type MembershipPlan,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniFeedbackService,
  AdeniModalComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
  type AdeniPillTone,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { BusinessSessionService } from "../../core/services/business-session.service";

type EnrolDraft = {
  membershipPlanId: string;
  businessLocationId: string;
  paymentMethod: "till" | "transfer";
  note: string;
};

function newKey(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

/**
 * Customer detail ? Memberships tab (Sprint 37.2-B): list this customer's memberships with current-cycle credits,
 * enrol at the till / by transfer, and (owner-only) freeze / resume / cancel. Credits are sessions, never money.
 */
@Component({
  selector: "app-customer-memberships-panel",
  standalone: true,
  imports: [FormsModule, AdeniCarbonIconComponent, AdeniModalComponent, AdeniPillComponent, AdeniPortalEmptyStateComponent],
  templateUrl: "./customer-memberships-panel.component.html",
  styleUrl: "./customer-memberships-panel.component.scss",
})
export class CustomerMembershipsPanelComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly session = inject(BusinessSessionService);

  readonly customerId = input.required<string>();

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly memberships = signal<CustomerMembership[]>([]);
  readonly plans = signal<MembershipPlan[]>([]);
  readonly locations = signal<BusinessLocation[]>([]);
  readonly enrolDraft = signal<EnrolDraft | null>(null);
  readonly activePlans = computed(() => this.plans().filter((p) => p.isActive));
  readonly isOwner = computed(() => this.session.permissionRole() === "owner");
  readonly activeMembershipCount = computed(
    () => this.memberships().filter((m) => m.status === "active" || m.status === "past_due").length,
  );
  readonly totalCycleSessionsLeft = computed(() =>
    this.memberships().reduce(
      (sum, m) => sum + m.balances.reduce((line, b) => line + b.remaining, 0),
      0,
    ),
  );
  private enrolKey = newKey();

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const [list, plans, locations] = await Promise.all([
          client.retention.listCustomerMemberships(this.customerId()),
          client.retention.listMembershipPlans(true).catch(() => ({ items: [] as MembershipPlan[] })),
          client.getTenantLocations().catch(() => [] as BusinessLocation[]),
        ]);
        this.memberships.set(list.items);
        this.plans.set(plans.items);
        this.locations.set(locations);
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.memberships.customerCouldNotLoad"));
      this.memberships.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  membershipProgress(m: CustomerMembership): { remaining: number; granted: number; deg: string } | null {
    const granted = m.balances.reduce((sum, b) => sum + b.granted, 0);
    const remaining = m.balances.reduce((sum, b) => sum + b.remaining, 0);
    if (granted <= 0) {
      return null;
    }
    return { remaining, granted, deg: `${(remaining / granted) * 360}deg` };
  }

  balancePercent(b: CustomerMembership["balances"][number]): number {
    if (b.granted <= 0) {
      return 0;
    }
    return Math.max(0, Math.min(100, Math.round((b.remaining / b.granted) * 100)));
  }

  price(m: CustomerMembership): string {
    return formatPrice(m.price, m.currency);
  }

  formatPlanPrice(plan: MembershipPlan): string {
    return formatPrice(plan.price, plan.currency);
  }

  when(iso: string): string {
    return formatSlotTime(iso);
  }

  intervalLabel(m: CustomerMembership): string {
    switch (m.billingInterval) {
      case "quarterly":
        return this.tr("businessApp.memberships.intervalQuarterly");
      case "annually":
        return this.tr("businessApp.memberships.intervalAnnually");
      default:
        return this.tr("businessApp.memberships.intervalMonthly");
    }
  }

  cycleEndLabel(m: CustomerMembership): string {
    const date = this.when(m.currentCycleEnd ?? "");
    return m.status === "cancel_pending" || m.status === "cancelled"
      ? this.tr("businessApp.memberships.cycleEnds", { date })
      : this.tr("businessApp.memberships.renews", { date });
  }

  statusLabel(m: CustomerMembership): string {
    switch (m.status) {
      case "pending":
        return this.tr("businessApp.memberships.statusPending");
      case "active":
        return this.tr("businessApp.memberships.statusActive");
      case "past_due":
        return this.tr("businessApp.memberships.statusPastDue");
      case "frozen":
        return this.tr("businessApp.memberships.statusFrozen");
      case "cancel_pending":
        return this.tr("businessApp.memberships.statusCancelPending");
      case "cancelled":
        return this.tr("businessApp.memberships.statusCancelled");
      default:
        return this.tr("businessApp.memberships.statusExpired");
    }
  }

  statusTone(m: CustomerMembership): AdeniPillTone {
    switch (m.status) {
      case "active":
        return "success";
      case "pending":
      case "past_due":
      case "frozen":
      case "cancel_pending":
        return "warning";
      case "cancelled":
        return "danger";
      default:
        return "neutral";
    }
  }

  isLive(m: CustomerMembership): boolean {
    return m.status !== "cancelled" && m.status !== "expired";
  }

  canManage(m: CustomerMembership): boolean {
    return this.isOwner() && this.isLive(m) && m.status !== "cancel_pending";
  }

  // ---- Enrol ----

  openEnrol(): void {
    this.enrolKey = newKey();
    this.enrolDraft.set({
      membershipPlanId: this.activePlans()[0]?.id ?? "",
      businessLocationId: this.locations().find((l) => l.isPrimary)?.id ?? this.locations()[0]?.id ?? "",
      paymentMethod: "till",
      note: "",
    });
  }

  closeEnrol(): void {
    this.enrolDraft.set(null);
  }

  patchEnrol(patch: Partial<EnrolDraft>): void {
    // A changed plan/method/location/note is a different request, so it needs a fresh Idempotency-Key.
    this.enrolKey = newKey();
    this.enrolDraft.update((d) => (d ? { ...d, ...patch } : d));
  }

  canEnrol(): boolean {
    const d = this.enrolDraft();
    return !!d && !!d.membershipPlanId && !!d.businessLocationId && !this.busy();
  }

  async confirmEnrol(): Promise<void> {
    const draft = this.enrolDraft();
    if (!draft || !this.canEnrol()) return;
    const plan = this.activePlans().find((p) => p.id === draft.membershipPlanId);
    const subject = plan?.name ?? this.tr("businessApp.memberships.enrol");
    if (!(await this.confirmDialog.confirmKind("grant", { subject }))) {
      return;
    }
    this.busy.set(true);
    try {
      const enrolled = await this.api.withAuthorizedClient((client) =>
        client.retention.enrolMembership(
          this.customerId(),
          {
            membershipPlanId: draft.membershipPlanId,
            businessLocationId: draft.businessLocationId,
            paymentMethod: draft.paymentMethod,
            note: draft.note.trim() || undefined,
          },
          this.enrolKey,
        ),
      );
      this.memberships.update((list) => [enrolled, ...list.filter((m) => m.id !== enrolled.id)]);
      this.enrolKey = newKey();
      this.closeEnrol();
      this.feedback.success(this.tr("businessApp.memberships.enrolled", { name: enrolled.name }));
    } catch (caught) {
      // Keep the same Idempotency-Key so a retry of an unchanged request cannot double-enrol.
      this.feedback.error(this.apiErr(caught, "businessApp.memberships.couldNotEnrol"));
    } finally {
      this.busy.set(false);
    }
  }

  // ---- Owner actions ----

  async freeze(m: CustomerMembership): Promise<void> {
    if (!(await this.confirmDialog.confirm({
      title: this.tr("businessApp.memberships.freeze"),
      message: this.tr("common.confirm.submit.message", { subject: m.name }),
      confirmLabel: this.tr("businessApp.memberships.freeze"),
    }))) {
      return;
    }
    await this.run(m, (client) => client.retention.freezeCustomerMembership(m.id), "businessApp.memberships.frozenDone");
  }

  async unfreeze(m: CustomerMembership): Promise<void> {
    if (!(await this.confirmDialog.confirmKind("reactivate", { subject: m.name, confirmLabel: this.tr("businessApp.memberships.unfreeze") }))) {
      return;
    }
    await this.run(m, (client) => client.retention.unfreezeCustomerMembership(m.id), "businessApp.memberships.unfrozenDone");
  }

  async cancel(m: CustomerMembership): Promise<void> {
    const immediate = m.cancellationPolicy === "immediate" || m.status === "pending";
    const ok = await this.confirmDialog.confirm({
      title: this.tr("businessApp.memberships.cancelTitle"),
      message: immediate
        ? this.tr("businessApp.memberships.cancelMessageImmediate", { name: m.name })
        : this.tr("businessApp.memberships.cancelMessageEndOfCycle", { name: m.name, date: this.when(m.currentCycleEnd ?? m.createdAt) }),
      confirmLabel: this.tr("businessApp.memberships.cancelMembership"),
      danger: true,
    });
    if (!ok) return;
    await this.run(m, (client) => client.retention.cancelCustomerMembership(m.id, undefined), "businessApp.memberships.cancelledDone");
  }

  private async run(
    m: CustomerMembership,
    call: (client: AdeniApiClient) => Promise<CustomerMembership>,
    successKey: string,
  ): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      const updated = await this.api.withAuthorizedClient(call);
      this.memberships.update((list) => list.map((x) => (x.id === m.id ? updated : x)));
      this.feedback.success(this.tr(successKey));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.memberships.couldNotUpdate"));
    } finally {
      this.busy.set(false);
    }
  }
}
