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
import type { LocaleId } from "@adeni/shared";
import {
  AdeniBrandLogoComponent,
  AdeniPortalNavIconComponent,
  AdeniLocaleService,
  AdeniStaffShellFooterComponent,
  AdeniStaffSidebarBrandComponent,
  injectAdeniTranslate,
} from "@adeni/ui";
import { filter, map, startWith } from "rxjs";
import { CustomerThemeService, type CustomerThemePreference } from "../../core/customer-theme.service";
import { crumbsForCustomerUrl } from "../customer-app-breadcrumbs";
import { CUSTOMER_NAV_GROUPS } from "../customer-nav-groups";
import { CustomerAppSessionService } from "../services/customer-app-session.service";
import { CustomerMessagingBadgeService } from "../services/customer-messaging-badge.service";
import { CustomerMessagingShellService } from "../services/customer-messaging-shell.service";
import { CustomerMessagesShortcutComponent } from "./customer-messages-shortcut.component";

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
    AdeniPortalNavIconComponent,
    CustomerMessagesShortcutComponent,
    AdeniStaffShellFooterComponent,
  ],
  templateUrl: "./customer-portal-shell.component.html",
  styleUrl: "./customer-portal-shell.component.scss",
})
export class CustomerPortalShellComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly session = inject(CustomerAppSessionService);
  protected readonly badge = inject(CustomerMessagingBadgeService);
  private readonly router = inject(Router);
  private readonly localeService = inject(AdeniLocaleService);
  private readonly customerTheme = inject(CustomerThemeService);
  private readonly messagingShell = inject(CustomerMessagingShellService);
  private readonly meta = inject(Meta);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly destroyRef = inject(DestroyRef);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly navGroups = CUSTOMER_NAV_GROUPS;
  readonly collapsed = signal(false);
  readonly settingsOpen = signal(false);
  readonly userOpen = signal(false);
  readonly locales = this.localeService.locales;
  readonly locale = this.localeService.locale;
  readonly themePreference = this.customerTheme.preference;
  readonly theme = this.customerTheme.theme;
  readonly logoSurface = computed<AdeniBrandSurface>(() => (this.theme() === "dark" ? "dark" : "light"));

  readonly accountName = computed(() => this.session.displayName() || this.tr("customer.app.account"));
  readonly accountDetail = computed(() => this.session.email() ?? "");
  readonly accountInitials = computed(() => initialsFor(this.accountName()));

  private lastUrl = this.router.url;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  readonly crumbs = computed(() => crumbsForCustomerUrl((key) => this.tr(key), this.url()));

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
    this.messagingShell.start();
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

  toggleTheme(): void {
    this.customerTheme.toggle();
  }

  setTheme(preference: CustomerThemePreference): void {
    this.customerTheme.setPreference(preference);
  }

  setLocale(locale: LocaleId): void {
    this.localeService.setLocale(locale);
    this.settingsOpen.set(false);
  }

  toggleSettings(event: MouseEvent): void {
    event.stopPropagation();
    this.userOpen.set(false);
    this.settingsOpen.update((open) => !open);
  }

  toggleUser(event: MouseEvent): void {
    event.stopPropagation();
    this.settingsOpen.set(false);
    this.userOpen.update((open) => !open);
  }

  closeMenus(): void {
    this.userOpen.set(false);
    this.settingsOpen.set(false);
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
    if (this.settingsOpen() && !this.host.nativeElement.querySelector('[data-menu-root="settings"]')?.contains(target)) {
      this.settingsOpen.set(false);
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
