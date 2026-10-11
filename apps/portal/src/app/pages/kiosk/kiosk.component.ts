import { Component, HostListener, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";
import { AdeniApiClient } from "@adeni/api-client";
import type { KioskStatusResponse, QueueTicketResponse, ServiceOffering } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";

@Component({
  selector: "app-kiosk",
  standalone: true,
  imports: [FormsModule],
  templateUrl: "./kiosk.component.html",
  styleUrl: "./kiosk.component.scss",
})
export class KioskComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly router = inject(Router);
  private readonly config = inject(ADENI_PORTAL_CONFIG);

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly exiting = signal(false);
  readonly error = signal<string | null>(null);
  readonly status = signal<KioskStatusResponse | null>(null);
  readonly services = signal<ServiceOffering[]>([]);
  readonly ticket = signal<QueueTicketResponse | null>(null);
  readonly exitOpen = signal(false);
  readonly businessName = signal("Join the queue");

  displayName = "";
  phone = "";
  serviceOfferingId = "";
  exitPin = "";

  ngOnInit(): void {
    void this.bootstrap();
  }

  @HostListener("document:keydown.escape")
  onEscape(): void {
    this.openExit();
  }

  async bootstrap(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const [status, profile, services] = await Promise.all([
          client.getTenantKioskStatus(),
          client.getTenantProfile(),
          client.getTenantServices(),
        ]);
        this.status.set(status);
        this.businessName.set(profile.businessName || "Join the queue");
        this.services.set(services.filter((s) => s.isActive !== false));
        if (!status.queueEnabled) {
          this.error.set("Walk-in queue is not enabled for this business.");
        } else if (!status.slug) {
          this.error.set("Add a primary location slug before opening kiosk.");
        }
      });
    } catch {
      this.error.set("Could not open kiosk.");
    } finally {
      this.loading.set(false);
    }
  }

  async join(): Promise<void> {
    const slug = this.status()?.slug;
    if (!slug || !this.displayName.trim() || !this.phone.trim()) {
      this.error.set("Enter your name and phone to join.");
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      const client = new AdeniApiClient({ baseUrl: this.config.apiBaseUrl });
      const ticket = await client.joinPublicQueue(slug, {
        displayName: this.displayName.trim(),
        phone: this.phone.trim(),
        serviceOfferingId: this.serviceOfferingId || undefined,
        partySize: 1,
      });
      this.ticket.set(ticket);
    } catch {
      this.error.set("Could not join the queue. Ask the front desk for help.");
    } finally {
      this.submitting.set(false);
    }
  }

  joinAnother(): void {
    this.ticket.set(null);
    this.displayName = "";
    this.phone = "";
    this.serviceOfferingId = "";
    this.error.set(null);
  }

  openExit(): void {
    this.exitOpen.set(true);
    this.exitPin = "";
    this.error.set(null);
  }

  cancelExit(): void {
    this.exitOpen.set(false);
    this.exitPin = "";
  }

  async confirmExit(): Promise<void> {
    const hasPin = this.status()?.hasPin === true;
    if (!hasPin) {
      if (!window.confirm("Leave kiosk mode?")) {
        return;
      }
      await this.router.navigateByUrl("/queue");
      return;
    }

    if (!/^\d{4,8}$/.test(this.exitPin.trim())) {
      this.error.set("Enter the staff PIN.");
      return;
    }

    this.exiting.set(true);
    this.error.set(null);
    try {
      const result = await this.api.withAuthorizedClient((client) =>
        client.verifyTenantKioskPin(this.exitPin.trim()),
      );
      if (!result.ok) {
        this.error.set("Incorrect PIN.");
        return;
      }
      await this.router.navigateByUrl("/queue");
    } catch {
      this.error.set("Could not verify PIN.");
    } finally {
      this.exiting.set(false);
    }
  }
}
