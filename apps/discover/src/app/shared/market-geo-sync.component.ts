import { Component, inject, OnInit } from "@angular/core";
import { MarketContextService } from "../core/services/market-context.service";

@Component({
  selector: "app-market-geo-sync",
  standalone: true,
  template: "",
})
export class MarketGeoSyncComponent implements OnInit {
  private readonly market = inject(MarketContextService);

  ngOnInit(): void {
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
