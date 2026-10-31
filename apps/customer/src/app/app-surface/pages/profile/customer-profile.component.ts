import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  AdeniCarbonIconComponent,
  AdeniLocaleService,
  AdeniPortalNavIconComponent,
  PortalPageComponent,
  injectAdeniTranslate,
} from "@adeni/ui";
import {
  resolveBusinessPortalWelcomeUrl,
  type CustomerVerificationResponse,
} from "@adeni/shared";
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
  protected readonly businessPortalUrl = resolveBusinessPortalWelcomeUrl(
    inject(ADENI_DISCOVER_CONFIG).portalAppUrl,
  );
  private readonly api = inject(CustomerApiService);
  private readonly locale = inject(AdeniLocaleService);

  readonly verification = signal<CustomerVerificationResponse | null>(null);

  readonly trustStepsDone = computed(() => {
    const v = this.verification();
    if (!v) {
      return 0;
    }
    return (v.emailVerified ? 1 : 0) + (v.phoneVerified ? 1 : 0);
  });

  readonly trustProgressDeg = computed(() => `${(this.trustStepsDone() / 2) * 360}deg`);

  trustHeadlineKey(): string {
    const done = this.trustStepsDone();
    if (done >= 2) {
      return "customer.app.profile.trustHeadlineComplete";
    }
    if (done === 1) {
      return "customer.app.profile.trustHeadlinePartial";
    }
    return "customer.app.profile.trustHeadlineStart";
  }

  trustSubtitleKey(): string {
    const v = this.verification();
    if (!v) {
      return "customer.app.profile.trustSubStart";
    }
    if (v.emailVerified && v.phoneVerified) {
      return "customer.app.profile.trustSubComplete";
    }
    if (v.emailVerified && !v.phoneVerified) {
      return "customer.app.profile.trustSubPartialEmail";
    }
    if (!v.emailVerified && v.phoneVerified) {
      return "customer.app.profile.trustSubPartialPhone";
    }
    return "customer.app.profile.trustSubStart";
  }

  formatVerifiedAt(iso: string | null | undefined): string | null {
    if (!iso) {
      return null;
    }
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) {
      return null;
    }
    return new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: "medium" }).format(date);
  }

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
