import { DatePipe } from "@angular/common";
import {
  Component,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Router, RouterLink } from "@angular/router";
import { Subscription } from "rxjs";
import type { AdeniApiClient } from "@adeni/api-client";
import type {
  BusinessProfile,
  QueueExpectedArrival,
  QueueTicketResponse,
  ServiceOffering,
  StaffMember,
  WalkInStatus,
} from "@adeni/shared";
import { combineGuestName, hasCapability, optionalEmail } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniModalComponent,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { environment } from "../../../environments/environment";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
import { TenantOperationsRealtimeService } from "@adeni/ui";
import { PortalSessionService } from "../../core/services/portal-session.service";
import { mergeTenantEntitlements } from "@adeni/shared";

const REFRESH_MS = 15_000;

@Component({
  selector: "app-queue",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniModalComponent,
    RouterLink,
    DatePipe,
  ],
  templateUrl: "./queue.component.html",
  styleUrl: "./queue.component.scss",
})
export class QueueComponent implements OnInit, OnDestroy {
  private readonly api = inject(BusinessApiService);
  private readonly session = inject(PortalSessionService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly router = inject(Router);
  private readonly operationsRealtime = inject(TenantOperationsRealtimeService);
  private readonly activeLocation = inject(PortalActiveLocationService);

  private refreshTimer: ReturnType<typeof setInterval> | null = null;
  private branchSub: Subscription | null = null;
  private opsSub: { unsubscribe(): void } | null = null;

  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly error = signal<string | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly tickets = signal<QueueTicketResponse[]>([]);
  readonly expectedArrivals = signal<QueueExpectedArrival[]>([]);
  readonly walkInStatus = signal<WalkInStatus | null>(null);
  readonly services = signal<ServiceOffering[]>([]);
  readonly staff = signal<StaffMember[]>([]);
  readonly actionId = signal<string | null>(null);
  readonly adding = signal(false);
  readonly showAddModal = signal(false);
  readonly lastRefreshedAt = signal<Date | null>(null);

  guestFirstName = "";
  guestLastName = "";
  guestPhone = "";
  guestEmail = "";
  serviceOfferingId = "";
  partySize = 1;
  walkInNotes = "";
  assignStaffMemberId = "";
  startStaffMemberId = "";

  ngOnInit(): void {
    void this.session.ensureLoaded();
    void this.load(true);
    this.refreshTimer = setInterval(() => void this.load(false), REFRESH_MS);
    this.opsSub = this.operationsRealtime.queueFloorChanged$.subscribe(() => {
      void this.load(false);
    });
    this.branchSub = this.activeLocation.selectionChanged$.subscribe(() => {
      void this.activeLocation.runOnBranchSwitch(() => this.load(false));
    });
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
    }
    this.opsSub?.unsubscribe();
    this.branchSub?.unsubscribe();
  }

  pageDescription(): string {
    if (this.session.can("portal.bookings.self") && !this.session.can("portal.bookings")) {
      return "Your assigned walk-ins — call, start service, and hand off to payment.";
    }
    return "Live floor board for walk-ins — refresh every 15s or use Refresh now.";
  }

  supportsQueue(): boolean {
    return hasCapability(this.profile()?.capabilities, "walk_in_queue");
  }

  canManageAll(): boolean {
    return this.session.can("portal.bookings");
  }

  waiting(): QueueTicketResponse[] {
    return sortByPosition(this.tickets().filter((t) => t.status === "waiting"));
  }

  called(): QueueTicketResponse[] {
    return this.tickets().filter((t) => t.status === "called");
  }

  inService(): QueueTicketResponse[] {
    return this.tickets().filter((t) => t.status === "in_service");
  }

  finished(): QueueTicketResponse[] {
    return this.tickets().filter(
      (t) => t.status === "done" || t.status === "no_show" || t.status === "cancelled",
    );
  }

  publicJoinUrl(): string | null {
    const slug = primarySlug(this.profile());
    if (!slug) {
      return null;
    }
    const base = environment.discoverWebUrl.replace(/\/$/, "");
    return `${base}/q/${encodeURIComponent(slug)}`;
  }

  statusLabel(status: string): string {
    switch (status) {
      case "waiting":
        return "Waiting";
      case "called":
        return "Called";
      case "in_service":
        return "In service";
      case "done":
        return "Done";
      case "no_show":
        return "No-show";
      case "cancelled":
        return "Cancelled";
      default:
        return status;
    }
  }

  async load(initial: boolean): Promise<void> {
    if (initial) {
      this.loading.set(true);
    } else {
      this.refreshing.set(true);
    }
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const [profile, entitlements, usage] = await Promise.all([
          client.getTenantProfile(),
          client.getTenantEntitlements().catch(() => null),
          client.getTenantSubscriptionUsage().catch(() => null),
        ]);
        this.activeLocation.syncFromProfile(profile);
        const locationQueryId = this.activeLocation.tenantApiLocationId(
          mergeTenantEntitlements(entitlements, usage?.entitlements, profile.entitlements),
        );
        const floor = await client.getTenantQueueFloor(locationQueryId);
        const services = await client.getTenantServices();
        let staff: StaffMember[] = [];
        if (this.session.can("portal.bookings")) {
          staff = await client.listTenantStaff().catch(() => []);
        }
        this.profile.set(profile);
        this.tickets.set(floor.items);
        this.expectedArrivals.set(floor.expectedArrivals);
        this.walkInStatus.set(floor.walkInStatus);
        this.services.set(services.filter((s) => s.isActive !== false));
        this.staff.set(staff.filter((m) => m.isActive !== false));
      });
      this.lastRefreshedAt.set(new Date());
    } catch {
      if (initial) {
        this.error.set("Could not load the queue.");
        this.tickets.set([]);
      }
    } finally {
      this.loading.set(false);
      this.refreshing.set(false);
    }
  }

  async refreshNow(): Promise<void> {
    await this.load(false);
  }

  async openWalkInsFor90Minutes(): Promise<void> {
    try {
      const settings = await this.api.withAuthorizedClient((client) =>
        client.openWalkInBlock(90),
      );
      this.walkInStatus.set({
        acceptingNow: settings.acceptingNow,
        walkInsEnabled: settings.walkInsEnabled,
        acceptingUntil: settings.walkInAcceptingUntil ?? null,
        maxWaiting: settings.maxWalkInWaiting ?? null,
        calendarPolicy: settings.calendarPolicy,
      });
      this.feedback.success("Walk-ins open for the next 90 minutes.", "Queue");
    } catch {
      this.feedback.error("Could not open walk-ins.");
    }
  }

  walkInsAccepting(): boolean {
    return this.walkInStatus()?.acceptingNow ?? true;
  }

  openAddModal(): void {
    this.error.set(null);
    this.showAddModal.set(true);
  }

  closeAddModal(): void {
    this.showAddModal.set(false);
  }

  async copyJoinLink(): Promise<void> {
    const url = this.publicJoinUrl();
    if (!url) {
      return;
    }
    try {
      await navigator.clipboard.writeText(url);
      this.feedback.success("Join link copied.", "Queue");
    } catch {
      this.feedback.error("Could not copy link.");
    }
  }

  async addWalkIn(): Promise<void> {
    const displayName = combineGuestName(this.guestFirstName, this.guestLastName);
    if (!displayName || !this.guestPhone.trim()) {
      this.error.set("First name, last name, and phone are required.");
      return;
    }
    this.adding.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient((client) =>
        client.addTenantQueueTicket({
          displayName,
          phone: this.guestPhone.trim(),
          email: optionalEmail(this.guestEmail),
          serviceOfferingId: this.serviceOfferingId || undefined,
          partySize: Math.max(1, this.partySize),
          notes: this.walkInNotes.trim() || undefined,
          staffMemberId: this.assignStaffMemberId || undefined,
        }),
      );
      this.guestFirstName = "";
      this.guestLastName = "";
      this.guestPhone = "";
      this.guestEmail = "";
      this.serviceOfferingId = "";
      this.partySize = 1;
      this.walkInNotes = "";
      this.assignStaffMemberId = "";
      await this.load(false);
      this.closeAddModal();
      this.feedback.success("Walk-in added to the queue.", "Queue");
    } catch {
      this.error.set("Could not add walk-in.");
    } finally {
      this.adding.set(false);
    }
  }

  async callNext(): Promise<void> {
    const next = this.waiting()[0];
    if (!next) {
      return;
    }
    await this.call(next);
  }

  async call(ticket: QueueTicketResponse): Promise<void> {
    await this.run(ticket.id, (client) => client.callTenantQueueTicket(ticket.id));
  }

  async start(ticket: QueueTicketResponse): Promise<void> {
    const staffId = this.resolveStartStaffId(ticket);
    this.actionId.set(ticket.id);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient((client) =>
        client.startTenantQueueTicket(ticket.id, staffId),
      );
      await this.load(false);
    } catch {
      this.error.set("Could not update this ticket.");
    } finally {
      this.actionId.set(null);
    }
  }

  async complete(ticket: QueueTicketResponse): Promise<void> {
    this.actionId.set(ticket.id);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((client) =>
        client.completeTenantQueueTicket(ticket.id),
      );
      await this.load(false);
      if (updated.bookingId) {
        this.feedback.success("Service complete — open Bookings to collect payment.", "Queue");
      }
    } catch {
      this.error.set("Could not update this ticket.");
    } finally {
      this.actionId.set(null);
    }
  }

  async noShow(ticket: QueueTicketResponse): Promise<void> {
    await this.run(ticket.id, (client) => client.noShowTenantQueueTicket(ticket.id));
  }

  async cancel(ticket: QueueTicketResponse): Promise<void> {
    await this.run(ticket.id, (client) => client.cancelTenantQueueTicket(ticket.id));
  }

  openBooking(ticket: QueueTicketResponse): void {
    void this.router.navigate(["/bookings"], {
      queryParams: {
        tab: "upcoming",
        focus: ticket.bookingId ?? undefined,
      },
    });
  }

  resolveStartStaffId(ticket: QueueTicketResponse): string | undefined {
    return (
      this.startStaffMemberId ||
      ticket.staffMemberId ||
      this.session.staffMemberId() ||
      undefined
    );
  }

  private async run(
    id: string,
    action: (client: AdeniApiClient) => Promise<QueueTicketResponse>,
  ): Promise<void> {
    this.actionId.set(id);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient((client) => action(client));
      await this.load(false);
    } catch {
      this.error.set("Could not update this ticket.");
    } finally {
      this.actionId.set(null);
    }
  }
}

function sortByPosition(tickets: QueueTicketResponse[]): QueueTicketResponse[] {
  return [...tickets].sort((a, b) => a.position - b.position);
}

function primarySlug(profile: BusinessProfile | null): string | null {
  const locations = profile?.locations ?? [];
  const primary = locations.find((l) => l.isPrimary) ?? locations[0];
  return primary?.slug ?? null;
}
