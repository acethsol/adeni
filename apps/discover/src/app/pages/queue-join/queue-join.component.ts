import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { AdeniApiClient } from "@adeni/api-client";
import type { PublicBusinessProfile, QueueTicketResponse, ServiceOffering } from "@adeni/shared";
import { ADENI_DISCOVER_CONFIG } from "../../core/adeni-config";

@Component({
  selector: "app-queue-join",
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: "./queue-join.component.html",
  styleUrl: "./queue-join.component.scss",
})
export class QueueJoinComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly business = signal<PublicBusinessProfile | null>(null);
  readonly services = signal<ServiceOffering[]>([]);
  readonly ticket = signal<QueueTicketResponse | null>(null);
  readonly kioskMode = signal(false);

  displayName = "";
  phone = "";
  serviceOfferingId = "";

  ngOnInit(): void {
    const kiosk = this.route.snapshot.queryParamMap.get("kiosk");
    this.kioskMode.set(kiosk === "1" || kiosk === "true");
    void this.load();
  }

  joinAnother(): void {
    this.ticket.set(null);
    this.displayName = "";
    this.phone = "";
    this.serviceOfferingId = "";
    this.error.set(null);
  }

  async load(): Promise<void> {
    const slug = this.route.snapshot.paramMap.get("slug");
    if (!slug) {
      this.error.set("Business not found.");
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    try {
      const client = new AdeniApiClient({ baseUrl: this.config.apiBaseUrl });
      const [profile, services] = await Promise.all([
        client.getBusinessProfile(slug),
        client.getBusinessServices(slug),
      ]);
      this.business.set(profile);
      this.services.set(services.filter((s) => s.isActive !== false));
    } catch {
      this.error.set("Could not load this business.");
    } finally {
      this.loading.set(false);
    }
  }

  async join(): Promise<void> {
    const slug = this.route.snapshot.paramMap.get("slug");
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
      this.error.set("Could not join the queue. It may not be available here.");
    } finally {
      this.submitting.set(false);
    }
  }
}
