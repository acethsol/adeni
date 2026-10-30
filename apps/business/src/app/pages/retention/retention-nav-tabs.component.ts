import { Component, inject, OnInit, signal } from "@angular/core";
import { RouterLink, RouterLinkActive } from "@angular/router";
import { mergeTenantEntitlements, type TenantEntitlements } from "@adeni/shared";
import { injectAdeniTranslate } from "@adeni/ui";
import { filterBusinessTabs } from "../../core/business-access";
import { findBusinessNavItem } from "../../core/business-nav";
import { BusinessApiService } from "../../core/services/business-api.service";

type RetentionTab = { id: string; label: string; path: string };

/** Section tabs for Packages · Memberships · Loyalty under `/retention/*`. */
@Component({
  selector: "app-retention-nav-tabs",
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    @if (tabs().length > 1) {
      <nav class="retention-nav" [attr.aria-label]="tr('businessApp.retention.sectionNav')">
        <div class="adeni-tabs" role="tablist">
          @for (tab of tabs(); track tab.id) {
            <a
              role="tab"
              [routerLink]="tab.path"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: true }"
              #rla="routerLinkActive"
              [attr.aria-selected]="rla.isActive"
            >
              {{ tab.label }}
            </a>
          }
        </div>
      </nav>
    }
  `,
  styleUrl: "./retention-nav-tabs.component.scss",
})
export class RetentionNavTabsComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly api = inject(BusinessApiService);

  readonly tabs = signal<RetentionTab[]>([]);

  ngOnInit(): void {
    void this.loadTabs();
  }

  private async loadTabs(): Promise<void> {
    try {
      const entitlements = await this.api.withAuthorizedClient(async (client) => {
        const [planEntitlements, profile] = await Promise.all([
          client.getTenantEntitlements().catch(() => null),
          client.getTenantProfile().catch(() => null),
        ]);
        return mergeTenantEntitlements(planEntitlements, null, profile?.entitlements ?? null);
      });
      this.tabs.set(this.buildTabs(entitlements));
    } catch {
      this.tabs.set(this.buildTabs(null));
    }
  }

  private buildTabs(entitlements: TenantEntitlements | null): RetentionTab[] {
    const navTabs = findBusinessNavItem("/retention")?.item.tabs ?? [];
    return filterBusinessTabs(navTabs, entitlements).map((tab) => ({
      id: tab.id,
      label: this.tabLabel(tab.id),
      path: `/retention/${tab.id}`,
    }));
  }

  private tabLabel(id: string): string {
    switch (id) {
      case "packages":
        return this.tr("businessApp.retention.title");
      case "memberships":
        return this.tr("businessApp.memberships.title");
      case "loyalty":
        return this.tr("businessApp.loyalty.title");
      default:
        return id;
    }
  }
}
