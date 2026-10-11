import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";
import type { AdeniApiClient } from "@adeni/api-client";
import type {
  BusinessProfile,
  KioskStatusResponse,
  QueueTicketResponse,
  ServiceOffering,
} from "@adeni/shared";
import { hasCapability } from "@adeni/shared";
import { AdeniCarbonIconComponent, PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";

@Component({
  selector: "app-queue",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, AdeniCarbonIconComponent],
  templateUrl: "./queue.component.html",
  styleUrl: "./queue.component.scss",
})
export class QueueComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly router = inject(Router);
  private readonly config = inject(ADENI_PORTAL_CONFIG);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly tickets = signal<QueueTicketResponse[]>([]);
  readonly services = signal<ServiceOffering[]>([]);
  readonly kioskStatus = signal<KioskStatusResponse | null>(null);
  readonly actionId = signal<string | null>(null);
  readonly adding = signal(false);
  readonly savingPin = signal(false);

  guestName = "";
  guestPhone = "";
  serviceOfferingId = "";
  kioskPin = "";

  ngOnInit(): void {
    void this.load();
  }

  supportsQueue(): boolean {
    return hasCapability(this.profile()?.capabilities, "walk_in_queue");
  }

  publicKioskUrl(): string | null {
    const slug = this.kioskStatus()?.slug;
    const base = this.config.discoverWebUrl?.replace(/\/$/, "");
    if (!slug || !base) return null;
    return `${base}/q/${encodeURIComponent(slug)}?kiosk=1`;
  }

  waiting(): QueueTicketResponse[] {
    return this.tickets().filter((t) => t.status === "waiting");
  }

  active(): QueueTicketResponse[] {
    return this.tickets().filter((t) => t.status === "called" || t.status === "in_service");
  }

  finished(): QueueTicketResponse[] {
    return this.tickets().filter(
      (t) => t.status === "done" || t.status === "no_show" || t.status === "cancelled",
    );
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

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const [profile, tickets, services, kiosk] = await Promise.all([
          client.getTenantProfile(),
          client.getTenantQueue(),
          client.getTenantServices(),
          client.getTenantKioskStatus().catch(() => null),
        ]);
        this.profile.set(profile);
        this.tickets.set(tickets);
        this.services.set(services.filter((s) => s.isActive !== false));
        this.kioskStatus.set(kiosk);
      });
    } catch {
      this.error.set("Could not load the queue.");
      this.tickets.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  openKiosk(): void {
    void this.router.navigateByUrl("/kiosk");
  }

  async saveKioskPin(): Promise<void> {
    if (!/^\d{4,8}$/.test(this.kioskPin.trim())) {
      this.error.set("Exit PIN must be 4–8 digits.");
      return;
    }
    this.savingPin.set(true);
    this.error.set(null);
    try {
      const status = await this.api.withAuthorizedClient((client) =>
        client.setTenantKioskPin(this.kioskPin.trim()),
      );
      this.kioskStatus.set(status);
      this.kioskPin = "";
    } catch {
      this.error.set("Could not save exit PIN.");
    } finally {
      this.savingPin.set(false);
    }
  }

  async clearKioskPin(): Promise<void> {
    this.savingPin.set(true);
    this.error.set(null);
    try {
      const status = await this.api.withAuthorizedClient((client) =>
        client.setTenantKioskPin(null),
      );
      this.kioskStatus.set(status);
      this.kioskPin = "";
    } catch {
      this.error.set("Could not clear exit PIN.");
    } finally {
      this.savingPin.set(false);
    }
  }

  async addWalkIn(): Promise<void> {
    if (!this.guestName.trim() || !this.guestPhone.trim()) {
      this.error.set("Name and phone are required.");
      return;
    }
    this.adding.set(true);
    this.error.set(null);
    try {
      const created = await this.api.withAuthorizedClient((client) =>
        client.addTenantQueueTicket({
          displayName: this.guestName.trim(),
          phone: this.guestPhone.trim(),
          serviceOfferingId: this.serviceOfferingId || undefined,
          partySize: 1,
        }),
      );
      this.tickets.update((list) => [...list, created]);
      this.guestName = "";
      this.guestPhone = "";
      this.serviceOfferingId = "";
    } catch {
      this.error.set("Could not add walk-in.");
    } finally {
      this.adding.set(false);
    }
  }

  async callNext(): Promise<void> {
    const next = this.waiting()[0];
    if (!next) return;
    await this.run(next.id, (client) => client.callTenantQueueTicket(next.id));
  }

  async call(ticket: QueueTicketResponse): Promise<void> {
    await this.run(ticket.id, (client) => client.callTenantQueueTicket(ticket.id));
  }

  async start(ticket: QueueTicketResponse): Promise<void> {
    await this.run(ticket.id, (client) => client.startTenantQueueTicket(ticket.id));
  }

  async complete(ticket: QueueTicketResponse): Promise<void> {
    await this.run(ticket.id, (client) => client.completeTenantQueueTicket(ticket.id));
  }

  async noShow(ticket: QueueTicketResponse): Promise<void> {
    await this.run(ticket.id, (client) => client.noShowTenantQueueTicket(ticket.id));
  }

  private async run(
    id: string,
    action: (client: AdeniApiClient) => Promise<QueueTicketResponse>,
  ): Promise<void> {
    this.actionId.set(id);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((client) => action(client));
      this.tickets.update((list) => list.map((item) => (item.id === id ? updated : item)));
    } catch {
      this.error.set("Could not update this ticket.");
    } finally {
      this.actionId.set(null);
    }
  }
}
