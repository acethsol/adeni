import { Component, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import { formatSlotTime, type CustomerPackage } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniLocaleService,
  injectAdeniApiError,
  injectAdeniTranslate,
} from "@adeni/ui";
import { CustomerApiService } from "../../core/services/customer-api.service";

type PackageTone = "pending" | "active" | "inactive";

/** Signed-in customer's prepaid packages across businesses (read-only; credits are sessions, never money). */
@Component({
  selector: "app-my-packages",
  standalone: true,
  imports: [RouterLink, AdeniCarbonIconComponent],
  templateUrl: "./my-packages.component.html",
  styleUrl: "./my-packages.component.scss",
})
export class MyPackagesComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly apiErr = injectAdeniApiError();
  private readonly api = inject(CustomerApiService);
  private readonly localeService = inject(AdeniLocaleService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly packages = signal<CustomerPackage[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await this.api.withAuthorizedClient((c) => c.retention.myPackages());
      this.packages.set(result.items);
    } catch (caught) {
      this.packages.set([]);
      this.error.set(this.apiErr(caught, "discover.packages.loadError"));
    } finally {
      this.loading.set(false);
    }
  }

  when(iso: string): string {
    return formatSlotTime(iso, this.localeService.locale());
  }

  tone(p: CustomerPackage): PackageTone {
    if (p.status === "active") return "active";
    if (p.status === "pending_payment") return "pending";
    return "inactive";
  }

  usedPercent(used: number, granted: number): number {
    if (granted <= 0) return 0;
    return Math.min(100, Math.max(0, Math.round((used / granted) * 100)));
  }

  statusLabel(p: CustomerPackage): string {
    switch (p.status) {
      case "pending_payment":
        return this.tr("discover.packages.statusPending");
      case "active":
        return this.tr("discover.packages.statusActive");
      case "expired":
        return this.tr("discover.packages.statusExpired");
      default:
        return this.tr("discover.packages.statusVoided");
    }
  }
}
