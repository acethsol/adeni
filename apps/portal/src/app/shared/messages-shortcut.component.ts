import { Component, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { NavigationEnd, Router, RouterLink } from "@angular/router";
import { filter, Subscription } from "rxjs";
import { hasTenantEntitlement } from "@adeni/shared";
import { BusinessApiService } from "../core/services/business-api.service";
import { MessagingRealtimeService, injectAdeniTranslate } from "@adeni/ui";
import { PortalNavBadgesService } from "../core/services/portal-nav-badges.service";

@Component({
  selector: "app-messages-shortcut",
  standalone: true,
  imports: [RouterLink],
  template: `
    @if (visible()) {
      <a
        class="bell"
        routerLink="/messages"
        [attr.aria-label]="
          unreadCount() ? unreadCount() + ' unread messages' : tr('common.messages')
        "
      >
        <svg viewBox="0 0 32 32" width="20" height="20" aria-hidden="true">
          <path
            fill="currentColor"
            d="M17.74,30,16,29l4-7h6a2,2,0,0,0,2-2V8a2,2,0,0,0-2-2H4A2,2,0,0,0,2,8V20a2,2,0,0,0,2,2h8.4ZM4,8H28V20H19.16l-2.72,4.76L13.6,20H4Z"
          />
        </svg>
        @if (unreadCount(); as count) {
          <span class="badge">{{ count > 9 ? "9+" : count }}</span>
        }
      </a>
    }
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
      background: var(--interactive, #7f56ff);
      color: #fff;
      font-size: 0.625rem;
      font-weight: 700;
      line-height: 1rem;
      text-align: center;
    }
  `,
})
export class MessagesShortcutComponent implements OnInit, OnDestroy {
  protected readonly tr = injectAdeniTranslate();
  private readonly api = inject(BusinessApiService);
  private readonly router = inject(Router);
  private readonly navBadges = inject(PortalNavBadgesService);
  private readonly messagingRealtime = inject(MessagingRealtimeService);

  readonly visible = signal(false);
  readonly unreadCount = signal(0);
  private subs = new Subscription();

  ngOnInit(): void {
    void this.refresh();
    this.subs.add(
      this.router.events
        .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
        .subscribe(() => {
          void this.refresh();
        }),
    );
    this.subs.add(
      this.navBadges.refreshRequested$.subscribe(() => {
        void this.refresh();
      }),
    );
    this.subs.add(
      this.messagingRealtime.conversationUpdated$.subscribe(() => {
        void this.refreshUnreadOnly();
      }),
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private async refreshUnreadOnly(): Promise<void> {
    if (!this.visible()) {
      return;
    }
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const count = await client.getTenantMessageUnreadCount();
        this.unreadCount.set(count);
      });
    } catch {
      // keep last value
    }
  }

  private async refresh(): Promise<void> {
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const profile = await client.getTenantProfile();
        if (!hasTenantEntitlement(profile.entitlements, "messaging")) {
          this.visible.set(false);
          this.unreadCount.set(0);
          return;
        }
        this.visible.set(true);
        const count = await client.getTenantMessageUnreadCount();
        this.unreadCount.set(count);
      });
    } catch {
      this.visible.set(false);
      this.unreadCount.set(0);
    }
  }
}
