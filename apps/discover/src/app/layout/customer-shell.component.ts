import { Component, OnInit, inject, signal } from "@angular/core";
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import {
  AdeniBrandLockupComponent,
  AdeniConfirmHostComponent,
  AdeniGlobalLoadingPanelComponent,
  AdeniToastHostComponent,
  injectAdeniTranslate,
} from "@adeni/ui";
import {
  ADENI_DISCOVER_CONFIG,
  isAuth0Configured,
  isDiscoverCustomerDevMode,
} from "../core/adeni-config";
import { CustomerApiService } from "../core/services/customer-api.service";

/**
 * Signed-in customer chrome for my-bookings, intake, packages, gift cards and messages.
 * Deliberately lighter than the public shell: no discovery search, no marketing footer.
 */
@Component({
  selector: "app-customer-shell",
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    AdeniBrandLockupComponent,
    AdeniGlobalLoadingPanelComponent,
    AdeniToastHostComponent,
    AdeniConfirmHostComponent,
  ],
  templateUrl: "./customer-shell.component.html",
  styleUrl: "./customer-shell.component.scss",
})
export class CustomerShellComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly api = inject(CustomerApiService);
  private readonly router = inject(Router);

  readonly auth0Mode = isAuth0Configured(this.config);
  readonly devCustomerMode = isDiscoverCustomerDevMode(this.config);
  readonly signedIn = signal(this.devCustomerMode);

  ngOnInit(): void {
    if (this.auth0Mode) {
      void this.api.isLoggedIn().then((v) => this.signedIn.set(v));
    }
  }

  logout(): void {
    void this.api.logout();
  }

  login(): void {
    void this.api.login(this.router.url);
  }
}
