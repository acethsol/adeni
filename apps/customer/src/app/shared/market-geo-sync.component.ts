import { Component, inject, OnInit } from "@angular/core";
import { ActivatedRoute } from "@angular/router";
import { MarketContextService } from "../core/services/market-context.service";

@Component({
  selector: "app-market-geo-sync",
  standalone: true,
  template: "",
})
export class MarketGeoSyncComponent implements OnInit {
  private readonly market = inject(MarketContextService);
  private readonly route = inject(ActivatedRoute);

  ngOnInit(): void {
    const marketParam = this.route.snapshot.queryParamMap.get("market");
    if (marketParam) {
      this.market.bootstrap().then(() => this.market.applyMarketQueryParam(marketParam));
    }
    if (typeof navigator === "undefined" || !navigator.geolocation) {
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        this.market.setCoordinates({
          lat: position.coords.latitude,
          lng: position.coords.longitude,
        });
      },
      () => {
        // User denied or unavailable — keep cookie/default market center.
      },
      { maximumAge: 60_000, timeout: 8_000 },
    );
  }
}
