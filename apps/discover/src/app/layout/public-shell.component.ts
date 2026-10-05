import { Component, inject, OnInit, signal } from "@angular/core";
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { filter } from "rxjs";
import {
  ADENI_DISCOVER_CONFIG,
  isAuth0Configured,
  isDiscoverCustomerDevMode,
} from "../core/adeni-config";
import { CustomerApiService } from "../core/services/customer-api.service";
import { MarketContextService } from "../core/services/market-context.service";
import { MarketGeoSyncComponent } from "../shared/market-geo-sync.component";
import { AdeniBrandLockupComponent } from "@adeni/ui";

@Component({
  selector: "app-public-shell",
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MarketGeoSyncComponent,
    AdeniBrandLockupComponent,
  ],
  templateUrl: "./public-shell.component.html",
  styleUrl: "./public-shell.component.scss",
})
export class PublicShellComponent implements OnInit {
  readonly market = inject(MarketContextService);
  private readonly router = inject(Router);
  readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly customerApi = inject(CustomerApiService);

  readonly auth0Mode = isAuth0Configured(this.config);
  readonly devCustomerMode = isDiscoverCustomerDevMode(this.config);
  readonly signedIn = signal(this.devCustomerMode);

  ngOnInit(): void {
    this.syncMarketQuery();
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(() => this.syncMarketQuery());

    if (this.auth0Mode) {
      void this.customerApi.isLoggedIn().then((v) => this.signedIn.set(v));
    }
  }

  private syncMarketQuery(): void {
    const market = this.router.parseUrl(this.router.url).queryParams["market"];
    if (typeof market === "string" && market.trim()) {
      this.market.applyMarketQueryParam(market);
    }
  }

  login(): void {
    void this.customerApi.login(this.router.url);
  }

  logout(): void {
    void this.customerApi.logout();
  }
}
