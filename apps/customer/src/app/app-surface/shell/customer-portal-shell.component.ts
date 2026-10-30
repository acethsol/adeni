import {
  Component,
  computed,
  DestroyRef,
  ElementRef,
  HostListener,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { takeUntilDestroyed, toSignal } from "@angular/core/rxjs-interop";
import { Meta } from "@angular/platform-browser";
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import type { AdeniBrandSurface } from "@adeni/brand";
import {
  AdeniBrandLogoComponent,
  AdeniCarbonIconComponent,
  AdeniConfirmHostComponent,
  AdeniGlobalLoadingPanelComponent,
  AdeniStaffShellFooterComponent,
  AdeniStaffSidebarBrandComponent,
  AdeniToastHostComponent,
  injectAdeniTranslate,
} from "@adeni/ui";
import { filter, map, startWith } from "rxjs";
import { CUSTOMER_NAV_GROUPS, findCustomerNavItem } from "../customer-nav-groups";
import { CustomerAppSessionService } from "../services/customer-app-session.service";
import { CustomerMessagingBadgeService } from "../services/customer-messaging-badge.service";

const ROBOTS_NOINDEX = { name: "robots", content: "noindex,nofollow" } as const;
const SIDEBAR_STORAGE_KEY = "adeni.customerApp.sidebar.collapsed";

function initialsFor(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) {
    return "A";
  }
  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase();
  }
  return `${parts[0][0] ?? ""}${parts[parts.length - 1][0] ?? ""}`.toUpperCase();
}

function readSidebarCollapsed(): boolean {
  try {
    const stored = localStorage.getItem(SIDEBAR_STORAGE_KEY);
    if (stored === "1") {
      return true;
    }
    if (stored === "0") {
      return false;
    }
  } catch {
    // private mode
  }
  return window.matchMedia("(max-width: 900px)").matches;
}

/**
 * Signed-in consumer portal — same shell grid as the business app (sidebar + top bar + content).
 * Discover marketplace pages stay on the public SSR surface; `/app/discover` is the in-app entry only.
 */
@Component({
  selector: "app-customer-app-shell",
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    AdeniBrandLogoComponent,
    AdeniStaffSidebarBrandComponent,
    AdeniCarbonIconComponent,
    AdeniStaffShellFooterComponent,
    AdeniGlobalLoadingPanelComponent,
    AdeniToastHostComponent,
    AdeniConfirmHostComponent,
  ],
  templateUrl: "./customer-portal-shell.component.html",
  styleUrl: "./customer-portal-shell.component.scss",
})
export class CustomerPortalShellComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly session = inject(CustomerAppSessionService);
  protected readonly badge = inject(CustomerMessagingBadgeService);
  private readonly router = inject(Router);
  private readonly meta = inject(Meta);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly destroyRef = inject(DestroyRef);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly navGroups = CUSTOMER_NAV_GROUPS;
  readonly collapsed = signal(false);
  readonly logoSurface = computed<AdeniBrandSurface>(() => "light");
  readonly userOpen = signal(false);

  readonly accountName = computed(() => this.session.displayName() || this.tr("customer.app.account"));
  readonly accountDetail = computed(() => this.session.email() ?? "");
  readonly accountInitials = computed(() => initialsFor(this.accountName()));

  private lastUrl = this.router.url;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      startWith(null),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  readonly crumbs = computed(() => {
    const match = findCustomerNavItem(this.url());
    if (!match) {
      return [{ label: this.tr("customer.app.nav.dashboard") }];
    }
    return [
      { label: this.tr(match.group.labelKey) },
      { label: this.tr(match.item.labelKey), path: match.item.path },
    ];
  });

  constructor() {
    this.meta.updateTag(ROBOTS_NOINDEX);
    this.destroyRef.onDestroy(() => this.meta.removeTag("name='robots'"));
  }

  ngOnInit(): void {
    this.session.start();
    if (!this.browser) {
      return;
    }

    this.collapsed.set(readSidebarCollapsed());
    void this.badge.refresh(true);
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((event) => {
        this.closeMenus();
        const leftMessages =
          this.lastUrl.startsWith("/app/messages") && !event.urlAfterRedirects.startsWith("/app/messages");
        this.lastUrl = event.urlAfterRedirects;
        void this.badge.refresh(leftMessages);
      });
  }

  toggleSidebar(): void {
    this.setCollapsed(!this.collapsed());
  }

  collapseSidebar(): void {
    this.setCollapsed(true);
  }

  toggleUser(event: MouseEvent): void {
    event.stopPropagation();
    this.userOpen.update((open) => !open);
  }

  closeMenus(): void {
    this.userOpen.set(false);
  }

  logout(): void {
    this.closeMenus();
    this.badge.clear();
    this.session.logout();
  }

  @HostListener("document:click", ["$event"])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as Node;
    if (this.userOpen() && !this.host.nativeElement.querySelector('[data-menu-root="user"]')?.contains(target)) {
      this.userOpen.set(false);
    }
  }

  @HostListener("document:keydown.escape")
  onEscape(): void {
    this.closeMenus();
  }

  private setCollapsed(next: boolean): void {
    this.collapsed.set(next);
    if (!this.browser) {
      return;
    }
    try {
      localStorage.setItem(SIDEBAR_STORAGE_KEY, next ? "1" : "0");
    } catch {
      // ignore
    }
  }
}
