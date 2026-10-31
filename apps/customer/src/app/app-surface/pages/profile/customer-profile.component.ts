import { Component, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  AdeniCarbonIconComponent,
  AdeniPortalNavIconComponent,
  PortalPageComponent,
  injectAdeniTranslate,
} from "@adeni/ui";
import type { CustomerVerificationResponse } from "@adeni/shared";
import { ADENI_DISCOVER_CONFIG } from "../../../core/adeni-config";
import { CustomerApiService } from "../../../core/services/customer-api.service";
import { CustomerAppSessionService } from "../../services/customer-app-session.service";

@Component({
  selector: "app-customer-profile",
  standalone: true,
  imports: [
    RouterLink,
    PortalPageComponent,
    AdeniCarbonIconComponent,
    AdeniPortalNavIconComponent,
  ],
  templateUrl: "./customer-profile.component.html",
  styleUrl: "./customer-profile.component.scss",
})
export class CustomerProfileComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly session = inject(CustomerAppSessionService);
  protected readonly businessPortalUrl = inject(ADENI_DISCOVER_CONFIG).portalAppUrl.trim() || null;
  private readonly api = inject(CustomerApiService);

  readonly verification = signal<CustomerVerificationResponse | null>(null);

  ngOnInit(): void {
    if (!this.session.auth0Mode && !this.session.devCustomerMode) {
      return;
    }

    void this.api.withAuthorizedClient(async (client) => {
      try {
        this.verification.set(await client.getMeVerification());
      } catch {
        this.verification.set(null);
      }
    });
  }
}
