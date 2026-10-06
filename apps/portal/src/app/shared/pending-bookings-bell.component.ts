import { Component, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { NavigationEnd, Router, RouterLink } from "@angular/router";
import { filter, Subscription } from "rxjs";
import { BusinessApiService } from "../core/services/business-api.service";

const PENDING_STATUS = 0;

@Component({
  selector: "app-pending-bookings-bell",
  standalone: true,
  imports: [RouterLink],
  template: `
    <a
      class="bell"
      routerLink="/bookings"
      [queryParams]="{ tab: 'pending' }"
      [attr.aria-label]="
        pendingCount() ? pendingCount() + ' pending bookings' : 'Bookings'
      "
    >
      <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
        <path
          fill="currentColor"
          d="M12 22a2.5 2.5 0 0 0 2.45-2h-4.9A2.5 2.5 0 0 0 12 22Zm7-6V11a7 7 0 1 0-14 0v5l-2 2v1h18v-1l-2-2Z"
        />
      </svg>
      @if (pendingCount(); as count) {
        <span class="badge">{{ count > 9 ? "9+" : count }}</span>
      }
    </a>
  `,
  styles: `
    .bell {
      position: relative;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 3rem;
      height: 3rem;
      color: var(--text-muted);
      text-decoration: none;
    }

    .bell:hover {
      background: rgb(127 86 255 / 8%);
      color: var(--text);
    }

    .badge {
      position: absolute;
      top: 0.15rem;
      right: 0.15rem;
      min-width: 1rem;
      height: 1rem;
      padding: 0 0.25rem;
      border-radius: 999px;
      background: #dc2626;
      color: #fff;
      font-size: 0.625rem;
      font-weight: 700;
      line-height: 1rem;
      text-align: center;
    }
  `,
})
export class PendingBookingsBellComponent implements OnInit, OnDestroy {
  private readonly api = inject(BusinessApiService);
  private readonly router = inject(Router);

  readonly pendingCount = signal(0);
  private navSub: Subscription | null = null;

  ngOnInit(): void {
    void this.refresh();
    this.navSub = this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(() => {
        void this.refresh();
      });
  }

  ngOnDestroy(): void {
    this.navSub?.unsubscribe();
  }

  private async refresh(): Promise<void> {
    try {
      const items = await this.api.withAuthorizedClient((c) => c.getTenantBookings());
      const pending = items.filter((b) => b.status === PENDING_STATUS).length;
      this.pendingCount.set(pending);
    } catch {
      this.pendingCount.set(0);
    }
  }
}
