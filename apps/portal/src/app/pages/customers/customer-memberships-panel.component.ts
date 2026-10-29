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
import { PortalSessionService } from "../../core/services/portal-session.service";

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
  template: `
    <div class="memberships__toolbar">
      <p class="muted note">{{ tr('portal.memberships.creditsAreSessions') }}</p>
      <button type="button" class="btn btn-primary" [disabled]="loading() || !!error()" (click)="openEnrol()">
        <adeni-carbon-icon name="add" [size]="16" />
        {{ tr('portal.memberships.enrol') }}
      </button>
    </div>

    @if (loading()) {
      <p class="muted" role="status">{{ tr('toast.loading') }}</p>
    } @else if (error()) {
      <p class="error-banner" role="alert">{{ error() }}</p>
    } @else if (memberships().length === 0) {
      <adeni-portal-empty-state
        icon="user--filled"
        [title]="tr('portal.memberships.customerEmptyTitle')"
        [description]="tr('portal.memberships.customerEmptyDescription')"
      />
    } @else {
      <ul class="memberships">
        @for (m of memberships(); track m.id) {
          <li class="card membership">
            <div class="membership__head">
              <h3 class="membership__name">{{ m.name }}</h3>
              <adeni-pill [tone]="statusTone(m)" [compact]="true">{{ statusLabel(m) }}</adeni-pill>
            </div>
            <dl class="membership__meta muted">
              <div><dt>{{ tr('portal.memberships.price') }}</dt><dd>{{ price(m) }} / {{ intervalLabel(m) }}</dd></div>
              <div><dt>{{ tr('portal.memberships.since') }}</dt><dd>{{ when(m.activatedAt ?? m.createdAt) }}</dd></div>
              @if (m.currentCycleEnd) {
                <div>
                  <dt>{{ tr('portal.memberships.cycle', { number: m.currentCycleNumber }) }}</dt>
                  <dd>{{ cycleEndLabel(m) }}</dd>
                </div>
              }
            </dl>
            @if (m.balances.length > 0) {
              <table class="balances">
                <caption class="muted">{{ tr('portal.memberships.cycleCredits') }}</caption>
                <thead>
                  <tr>
                    <th scope="col">{{ tr('portal.retention.service') }}</th>
                    <th scope="col">{{ tr('portal.retention.granted') }}</th>
                    <th scope="col">{{ tr('portal.retention.used') }}</th>
                    <th scope="col">{{ tr('portal.retention.reserved') }}</th>
                    <th scope="col">{{ tr('portal.retention.remaining') }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (b of m.balances; track b.id) {
                    <tr>
                      <th scope="row">{{ b.serviceName }}</th>
                      <td>{{ b.granted }}</td>
                      <td>{{ b.used }}</td>
                      <td>{{ b.reserved }}</td>
                      <td><strong>{{ b.remaining }}</strong></td>
                    </tr>
                  }
                </tbody>
              </table>
            }
            @if (canManage(m)) {
              <div class="membership__actions">
                @if (m.status === 'active' && m.allowFreeze) {
                  <button type="button" class="btn" [disabled]="busy()" (click)="freeze(m)">
                    {{ tr('portal.memberships.freeze') }}
                  </button>
                }
                @if (m.status === 'frozen') {
                  <button type="button" class="btn" [disabled]="busy()" (click)="unfreeze(m)">
                    {{ tr('portal.memberships.unfreeze') }}
                  </button>
                }
                <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="cancel(m)">
                  {{ tr('portal.memberships.cancelMembership') }}
                </button>
              </div>
            } @else if (isLive(m) && !isOwner()) {
              <p class="muted note">{{ tr('portal.memberships.ownerOnlyHint') }}</p>
            }
          </li>
        }
      </ul>
    }

    <adeni-modal
      [open]="enrolDraft() !== null"
      [title]="tr('portal.memberships.enrolTitle')"
      [subtitle]="tr('portal.memberships.enrolSubtitle')"
      size="lg"
      [hasFooter]="false"
      (closed)="closeEnrol()"
    >
      @if (enrolDraft(); as draft) {
        @if (activePlans().length === 0) {
          <p class="muted">{{ tr('portal.memberships.noActivePlans') }}</p>
        } @else {
          <div class="field">
            <label for="enrol-plan">{{ tr('portal.memberships.selectPlan') }}</label>
            <select id="enrol-plan" [ngModel]="draft.membershipPlanId" (ngModelChange)="patchEnrol({ membershipPlanId: $event })">
              @for (plan of activePlans(); track plan.id) {
                <option [value]="plan.id">{{ plan.name }} — {{ formatPlanPrice(plan) }}</option>
              }
            </select>
          </div>
          <div class="field-row">
            <div class="field">
              <label for="enrol-location">{{ tr('portal.retention.location') }}</label>
              <select id="enrol-location" [ngModel]="draft.businessLocationId" (ngModelChange)="patchEnrol({ businessLocationId: $event })">
                @for (location of locations(); track location.id) {
                  <option [value]="location.id">{{ location.name || location.slug }}</option>
                }
              </select>
            </div>
            <div class="field">
              <label for="enrol-method">{{ tr('portal.retention.paymentMethod') }}</label>
              <select id="enrol-method" [ngModel]="draft.paymentMethod" (ngModelChange)="patchEnrol({ paymentMethod: $event })">
                <option value="till">{{ tr('portal.retention.till') }}</option>
                <option value="transfer">{{ tr('portal.retention.transfer') }}</option>
              </select>
            </div>
          </div>
          <div class="field">
            <label for="enrol-note">{{ tr('portal.retention.note') }}</label>
            <input id="enrol-note" maxlength="500" [ngModel]="draft.note" (ngModelChange)="patchEnrol({ note: $event })" />
          </div>
          <p class="muted">{{ tr('portal.retention.noOnlineCharge') }}</p>
        }
        <div class="actions">
          <button type="button" class="btn" (click)="closeEnrol()">
            <adeni-carbon-icon name="close" [size]="16" />
            {{ tr('common.cancel') }}
          </button>
          <button type="button" class="btn btn-primary" [disabled]="!canEnrol()" (click)="confirmEnrol()">
            <adeni-carbon-icon name="checkmark" [size]="16" />
            {{ tr('portal.memberships.enrol') }}
          </button>
        </div>
      }
    </adeni-modal>
  `,
  styles: [
    `
      .memberships__toolbar { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin-bottom: 1rem; }
      .memberships { list-style: none; margin: 0; padding: 0; display: grid; gap: 1rem; }
      .note { margin: 0; }
      .membership { padding: 1rem 1.25rem; }
      .membership__head { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; }
      .membership__name { margin: 0; font-size: 1rem; }
      .membership__meta { display: flex; flex-wrap: wrap; gap: 0.5rem 1.5rem; margin: 0.75rem 0; }
      .membership__meta div { display: flex; gap: 0.35rem; }
      .membership__meta dt, .membership__meta dd { margin: 0; }
      .membership__meta dt::after { content: ":"; }
      .membership__actions { display: flex; gap: 0.5rem; margin-top: 0.75rem; }
      .balances { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
      .balances caption { text-align: left; padding-bottom: 0.25rem; }
      .balances th, .balances td { padding: 0.35rem 0.5rem; text-align: right; border-top: 1px solid var(--cds-border-subtle, #e0e0e0); }
      .balances th:first-child { text-align: left; font-weight: 500; }
      .field { display: grid; gap: 0.25rem; margin-bottom: 0.75rem; }
      .field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
      .actions { display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 1rem; }
    `,
  ],
})
export class CustomerMembershipsPanelComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly session = inject(PortalSessionService);

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
      this.error.set(this.apiErr(caught, "portal.memberships.customerCouldNotLoad"));
      this.memberships.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  // ---- Display ----

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
        return this.tr("portal.memberships.intervalQuarterly");
      case "annually":
        return this.tr("portal.memberships.intervalAnnually");
      default:
        return this.tr("portal.memberships.intervalMonthly");
    }
  }

  cycleEndLabel(m: CustomerMembership): string {
    const date = this.when(m.currentCycleEnd ?? "");
    return m.status === "cancel_pending" || m.status === "cancelled"
      ? this.tr("portal.memberships.cycleEnds", { date })
      : this.tr("portal.memberships.renews", { date });
  }

  statusLabel(m: CustomerMembership): string {
    switch (m.status) {
      case "pending":
        return this.tr("portal.memberships.statusPending");
      case "active":
        return this.tr("portal.memberships.statusActive");
      case "past_due":
        return this.tr("portal.memberships.statusPastDue");
      case "frozen":
        return this.tr("portal.memberships.statusFrozen");
      case "cancel_pending":
        return this.tr("portal.memberships.statusCancelPending");
      case "cancelled":
        return this.tr("portal.memberships.statusCancelled");
      default:
        return this.tr("portal.memberships.statusExpired");
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
      this.feedback.success(this.tr("portal.memberships.enrolled", { name: enrolled.name }));
    } catch (caught) {
      // Keep the same Idempotency-Key so a retry of an unchanged request cannot double-enrol.
      this.feedback.error(this.apiErr(caught, "portal.memberships.couldNotEnrol"));
    } finally {
      this.busy.set(false);
    }
  }

  // ---- Owner actions ----

  async freeze(m: CustomerMembership): Promise<void> {
    await this.run(m, (client) => client.retention.freezeCustomerMembership(m.id), "portal.memberships.frozenDone");
  }

  async unfreeze(m: CustomerMembership): Promise<void> {
    await this.run(m, (client) => client.retention.unfreezeCustomerMembership(m.id), "portal.memberships.unfrozenDone");
  }

  async cancel(m: CustomerMembership): Promise<void> {
    const immediate = m.cancellationPolicy === "immediate" || m.status === "pending";
    const ok = await this.confirmDialog.confirm({
      title: this.tr("portal.memberships.cancelTitle"),
      message: immediate
        ? this.tr("portal.memberships.cancelMessageImmediate", { name: m.name })
        : this.tr("portal.memberships.cancelMessageEndOfCycle", { name: m.name, date: this.when(m.currentCycleEnd ?? m.createdAt) }),
      confirmLabel: this.tr("portal.memberships.cancelMembership"),
      danger: true,
    });
    if (!ok) return;
    await this.run(m, (client) => client.retention.cancelCustomerMembership(m.id, undefined), "portal.memberships.cancelledDone");
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
      this.feedback.error(this.apiErr(caught, "portal.memberships.couldNotUpdate"));
    } finally {
      this.busy.set(false);
    }
  }
}
