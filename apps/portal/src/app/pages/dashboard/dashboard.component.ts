import { Component, inject, OnInit, signal } from "@angular/core";
import type { BusinessProfile } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-dashboard",
  standalone: true,
  templateUrl: "./dashboard.component.html",
  styleUrl: "./dashboard.component.scss",
})
export class DashboardComponent implements OnInit {
  private readonly businessApi = inject(BusinessApiService);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly loadError = signal(false);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loadError.set(false);
    const profile = await this.businessApi.getTenantProfile();
    if (!profile) {
      this.loadError.set(true);
    }
    this.profile.set(profile);
  }
}
