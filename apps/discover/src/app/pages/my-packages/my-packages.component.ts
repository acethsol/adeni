import { Component, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import { formatSlotTime, type CustomerPackage } from "@adeni/shared";
import { AdeniLocaleService, injectAdeniApiError, injectAdeniTranslate } from "@adeni/ui";
import { CustomerApiService } from "../../core/services/customer-api.service";

/** Signed-in customer's prepaid packages across businesses (read-only; credits are sessions, never money). */
@Component({
  selector: "app-my-packages",
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="page">
      <header class="page-head">
        <p class="kicker">Adeni</p>
        <h1>{{ tr("discover.packages.myTitle") }}</h1>
        <p class="lede">{{ tr("discover.packages.myDescription") }}</p>
      </header>

      @if (loading()) {
        <p role="status">{{ tr("toast.loading") }}</p>
      } @else if (error()) {
        <div class="empty">
          <p role="alert">{{ error() }}</p>
          <button type="button" class="cds-btn cds-btn--primary" (click)="load()">{{ tr("discover.tryAgain") }}</button>
        </div>
      } @else if (packages().length === 0) {
        <div class="empty">
          <h2>{{ tr("discover.packages.myEmptyTitle") }}</h2>
          <p>{{ tr("discover.packages.myEmptyDescription") }}</p>
          <a class="cds-btn cds-btn--primary" routerLink="/discover">{{ tr("bookings.discoverBusinesses") }}</a>
        </div>
      } @else {
        <ul class="list">
          @for (p of packages(); track p.id) {
            <li class="card">
              <div class="title-row">
                <h2>{{ p.name }}</h2>
                <span class="status" [attr.data-tone]="p.status">{{ statusLabel(p) }}</span>
              </div>
              @if (p.businessName) {
                <p class="meta">{{ p.businessName }}</p>
              }
              <ul class="balances">
                @for (b of p.balances; track b.id) {
                  <li>
                    <strong>{{ b.serviceName }}</strong>
                    — {{ tr("discover.packages.left", { count: b.remaining }) }}
                    <span class="tiny">({{ tr("discover.packages.ofTotal", { used: b.used, granted: b.granted }) }})</span>
                  </li>
                }
              </ul>
              @if (p.expiresAt) {
                <p class="meta">{{ tr("discover.packages.expires", { date: when(p.expiresAt) }) }}</p>
              }
            </li>
          }
        </ul>
      }
    </div>
  `,
  styles: [
    `
      .page { width: min(46rem, 100%); margin: 0 auto; padding: 0 0 3rem; }
      .page-head { margin-bottom: 1.5rem; }
      .kicker { text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.75rem; opacity: 0.7; margin: 0; }
      .lede, .meta, .tiny { opacity: 0.75; }
      .list { list-style: none; margin: 0; padding: 0; display: grid; gap: 1rem; }
      .card { border: 1px solid var(--cds-border-subtle, #e0e0e0); background: var(--cds-layer-01, #fff); padding: 1rem 1.25rem; }
      .title-row { display: flex; justify-content: space-between; align-items: baseline; gap: 0.75rem; }
      .title-row h2 { margin: 0; font-size: 1.1rem; }
      .status { font-size: 0.8rem; padding: 0.15rem 0.5rem; background: var(--cds-layer-accent, #e0e0e0); }
      .status[data-tone="active"] { background: #defbe6; }
      .status[data-tone="pending_payment"] { background: #fcf4d6; }
      .balances { margin: 0.5rem 0; padding-left: 1.1rem; }
      .empty { text-align: center; padding: 2rem 0; }
    `,
  ],
})
export class MyPackagesComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly apiErr = injectAdeniApiError();
  private readonly api = inject(CustomerApiService);
  private readonly localeService = inject(AdeniLocaleService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly packages = signal<CustomerPackage[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await this.api.withAuthorizedClient((c) => c.retention.myPackages());
      this.packages.set(result.items);
    } catch (caught) {
      this.packages.set([]);
      this.error.set(this.apiErr(caught, "discover.packages.loadError"));
    } finally {
      this.loading.set(false);
    }
  }

  when(iso: string): string {
    return formatSlotTime(iso, this.localeService.locale());
  }

  statusLabel(p: CustomerPackage): string {
    switch (p.status) {
      case "pending_payment":
        return this.tr("discover.packages.statusPending");
      case "active":
        return this.tr("discover.packages.statusActive");
      case "expired":
        return this.tr("discover.packages.statusExpired");
      default:
        return this.tr("discover.packages.statusVoided");
    }
  }
}
