import { Component, inject, OnInit } from "@angular/core";
import { RouterLink } from "@angular/router";
import { ADENI_DISCOVER_CONFIG } from "../../core/adeni-config";
import { MarketContextService } from "../../core/services/market-context.service";
import { SeoService } from "../../core/services/seo.service";

@Component({
  selector: "app-home",
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./home.component.html",
  styleUrl: "./home.component.scss",
})
export class HomeComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly market = inject(MarketContextService);

  async ngOnInit(): Promise<void> {
    await this.market.bootstrap();
    const marketName = this.market.market()?.name ?? "Adeni";
    this.seo.update(
      {
        title: `Adeni — Book local services in ${marketName}`,
        description: `Discover and book trusted businesses near you on Adeni ${marketName}.`,
        canonicalPath: "/",
      },
      this.config.publicAppUrl,
    );
  }
}
