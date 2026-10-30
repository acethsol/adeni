import { Component, inject } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  AdeniCarbonIconComponent,
  AdeniPortalNavIconComponent,
  PortalPageComponent,
  injectAdeniTranslate,
} from "@adeni/ui";
import { ADENI_DISCOVER_CONFIG } from "../../../core/adeni-config";
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
export class CustomerProfileComponent {
  protected readonly tr = injectAdeniTranslate();
  protected readonly session = inject(CustomerAppSessionService);
  protected readonly businessPortalUrl = inject(ADENI_DISCOVER_CONFIG).portalAppUrl.trim() || null;
}
