import {
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { Meta } from "@angular/platform-browser";
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { filter } from "rxjs";
import {
  AdeniBrandLockupComponent,
  AdeniBrandLogoComponent,
  AdeniCarbonIconComponent,
  AdeniConfirmHostComponent,
  AdeniGlobalLoadingPanelComponent,
  AdeniToastHostComponent,
  injectAdeniTranslate,
} from "@adeni/ui";
import { CUSTOMER_DESKTOP_NAV, CUSTOMER_TAB_NAV } from "../customer-nav";
import { CustomerAppSessionService } from "../services/customer-app-session.service";
import { CustomerMessagingBadgeService } from "../services/customer-messaging-badge.service";

const ROBOTS_NOINDEX = { name: "robots", content: "noindex,nofollow" } as const;

/**
 * Signed-in customer chrome for everything under `/app/*`.
 * Desktop (≥ 900px): portal-inspired top bar with primary nav + account menu.
 * Below 900px: compact top bar + bottom tab bar (Home · Discover · Bookings · Messages · Profile).
 * Styles live in the global `.adeni-app` partial (`styles/_app-surface.scss`), not in this component.
 */
@Component({
  selector: "app-customer-app-shell",
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    AdeniBrandLockupComponent,
    AdeniBrandLogoComponent,
    AdeniCarbonIconComponent,
    AdeniGlobalLoadingPanelComponent,
    AdeniToastHostComponent,
    AdeniConfirmHostComponent,
  ],
  templateUrl: "./customer-app-shell.component.html",
})
export class CustomerAppShellComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly session = inject(CustomerAppSessionService);
  protected readonly badge = inject(CustomerMessagingBadgeService);
  private readonly router = inject(Router);
  private readonly meta = inject(Meta);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly destroyRef = inject(DestroyRef);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly desktopNav = CUSTOMER_DESKTOP_NAV;
  protected readonly tabNav = CUSTOMER_TAB_NAV;
  protected readonly menuOpen = signal(false);

  private lastUrl = this.router.url;

  constructor() {
    // Private surface: never indexable, regardless of how the document was served.
    this.meta.updateTag(ROBOTS_NOINDEX);
    this.destroyRef.onDestroy(() => this.meta.removeTag("name='robots'"));
  }

  ngOnInit(): void {
    this.session.start();
    if (!this.browser) {
      return;
    }

    void this.badge.refresh(true);
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((event) => {
        this.menuOpen.set(false);
        // Leaving the thread view can change what is unread, so bypass the throttle then.
        const leftMessages = this.lastUrl.startsWith("/app/messages") && !event.urlAfterRedirects.startsWith("/app/messages");
        this.lastUrl = event.urlAfterRedirects;
        void this.badge.refresh(leftMessages);
      });
  }

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected signOut(): void {
    this.menuOpen.set(false);
    this.badge.clear();
    this.session.logout();
  }

  protected signIn(): void {
    this.session.login(this.router.url);
  }

  @HostListener("document:click", ["$event"])
  protected onDocumentClick(event: MouseEvent): void {
    if (this.menuOpen() && !(this.host.nativeElement as HTMLElement).querySelector(".app-account")?.contains(event.target as Node)) {
      this.menuOpen.set(false);
    }
  }

  @HostListener("document:keydown.escape")
  protected onEscape(): void {
    this.menuOpen.set(false);
  }
}
