import { DecimalPipe } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import type { PublicBusinessProfile, ServiceOffering } from "@adeni/shared";
import { getCategoryLabel, resolveBusinessCoverImage } from "@adeni/shared";
import { DiscoverApiService } from "../../core/services/discover-api.service";

@Component({
  selector: "app-business-profile",
  standalone: true,
  imports: [RouterLink, DecimalPipe],
  templateUrl: "./business.component.html",
  styleUrl: "./business.component.scss",
})
export class BusinessProfileComponent implements OnInit {
  private readonly api = inject(DiscoverApiService);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<PublicBusinessProfile | null>(null);
  readonly services = signal<ServiceOffering[]>([]);

  readonly categoryLabel = getCategoryLabel;
  readonly coverFor = resolveBusinessCoverImage;

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get("slug");
      if (slug) {
        void this.load(slug);
      }
    });
  }

  async load(slug: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    const client = this.api.createClient();

    try {
      const [profile, services] = await Promise.all([
        client.getBusinessProfile(slug),
        client.getBusinessServices(slug).catch(() => [] as ServiceOffering[]),
      ]);
      this.profile.set(profile);
      this.services.set(services);
    } catch {
      this.error.set("Business not found or API unavailable.");
      this.profile.set(null);
      this.services.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
