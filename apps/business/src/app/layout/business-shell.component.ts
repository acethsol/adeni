import { CHROME, type ChromeKey } from "./business-shell-copy";
import { businessNavigationGroups, businessSearchResults, type SearchHit } from "./business-shell-navigation";
import { Component, computed, ElementRef, HostListener, inject, Injector, signal, viewChild } from "@angular/core";
import { takeUntilDestroyed, toSignal } from "@angular/core/rxjs-interop";
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import type { AuthService } from "@auth0/auth0-angular";
import type { AdeniBrandSurface } from "@adeni/brand";
import {
  formatTenantStatus,
  hasTenantEntitlement,
  permissionsForRole,
  type BusinessProfile,
  type LocaleId,
} from "@adeni/shared";
import {
  AdeniBrandLogoComponent,
  AdeniStaffSidebarBrandComponent,
  AdeniConfirmHostComponent,
  AdeniDocumentTitleStrategy,
  AdeniGlobalLoadingPanelComponent,
  AdeniLocaleService,
  AdeniStaffShellFooterComponent,
  AdeniToastHostComponent,
  injectAdeniTranslate,
} from "@adeni/ui";
import { filter, map, startWith } from "rxjs";
import { ADENI_PORTAL_CONFIG, isAuth0Configured, isBusinessPortalDevMode } from "../core/adeni-config";
import { resolveAuthService } from "../core/auth0-rxjs";
import { signOutPortalDev } from "../core/business-dev-session";
import {
  filterBusinessTabs,
  businessEntitlements,
} from "../core/business-access";
import {
  BUSINESS_NAV,
  activeBusinessTab,
  findBusinessNavItem,
  type BusinessNavGroup,
  type BusinessNavItem,
} from "../core/business-nav";
import { BusinessApiService } from "../core/services/business-api.service";
import { BusinessActiveLocationService } from "../core/services/business-active-location.service";
import { BusinessSessionService } from "../core/services/business-session.service";
import type { ThemePreference } from "../core/business-theme.service";
import { BusinessThemeService } from "../core/business-theme.service";
import {
  MessagingRealtimeService,
  PortalRealtimeBootstrapService,
  TenantOperationsRealtimeService,
} from "@adeni/ui";
import { BusinessNavBadgesService } from "../core/services/business-nav-badges.service";
import { MessagesShortcutComponent } from "../shared/messages-shortcut.component";
import { PendingBookingsBellComponent } from "../shared/pending-bookings-bell.component";
import { BusinessIconTooltipDirective } from "../shared/business-icon-tooltip.directive";
import { BusinessNavIconComponent } from "./business-nav-icon.component";
import { BusinessBranchSwitchComponent } from "./business-branch-switch.component";

const SIDEBAR_STORAGE_KEY = "adeni.portal.sidebar.collapsed";


function initialsFor(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) {
    return "B";
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
    // Storage can throw in private mode.
  }
  return window.matchMedia("(max-width: 900px)").matches;
}

@Component({
  selector: "app-business-shell",
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    AdeniBrandLogoComponent,
    AdeniStaffSidebarBrandComponent,
    AdeniStaffShellFooterComponent,
    AdeniGlobalLoadingPanelComponent,
    AdeniToastHostComponent,
    AdeniConfirmHostComponent,
    MessagesShortcutComponent,
    PendingBookingsBellComponent,
    BusinessNavIconComponent,
    BusinessIconTooltipDirective,
    BusinessBranchSwitchComponent,
  ],
  templateUrl: "./business-shell.component.html",
  styleUrl: "./business-shell.component.scss",
})
export class BusinessShellComponent {
  protected readonly tr = injectAdeniTranslate();
  private readonly localeService = inject(AdeniLocaleService);
  readonly locales = this.localeService.locales;
  readonly locale = this.localeService.locale;
  readonly formatTenantStatus = formatTenantStatus;
  readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly injector = inject(Injector);
  private readonly router = inject(Router);
  private readonly businessApi = inject(BusinessApiService);
  private readonly navBadges = inject(BusinessNavBadgesService);
  private readonly portalRealtime = inject(PortalRealtimeBootstrapService);
  private readonly messagingRealtime = inject(MessagingRealtimeService);
  private readonly operationsRealtime = inject(TenantOperationsRealtimeService);
  private readonly businessSession = inject(BusinessSessionService);
  readonly activeLocation = inject(BusinessActiveLocationService);
  private readonly documentTitleStrategy = inject(AdeniDocumentTitleStrategy);
  private readonly businessTheme = inject(BusinessThemeService);
  readonly auth: AuthService | null = isAuth0Configured(this.config)
    ? resolveAuthService(this.injector)
    : null;

  readonly devMode = isBusinessPortalDevMode(this.config);
  readonly auth0Mode = isAuth0Configured(this.config);
  readonly collapsed = signal(readSidebarCollapsed());
  readonly themePreference = this.businessTheme.preference;
  readonly theme = this.businessTheme.theme;
  readonly logoSurface = computed<AdeniBrandSurface>(() => (this.theme() === "dark" ? "dark" : "light"));
  readonly settingsOpen = signal(false);
  readonly userOpen = signal(false);
  readonly searchQuery = signal("");
  readonly searchOpen = signal(false);
  readonly searchIndex = signal(0);
  private readonly searchField = viewChild<ElementRef<HTMLInputElement>>("searchField");
  readonly profile = signal<BusinessProfile | null>(null);
  readonly businessEntitlementsFn = businessEntitlements;
  readonly profileLoaded = signal(false);
  readonly pendingBookings = signal(0);
  readonly unreadMessages = signal(0);
  readonly accountName = signal(this.devMode ? "Local business" : "Business");
  readonly accountDetail = signal(this.devMode ? this.config.devBusinessAuth0Sub : "");
  readonly accountInitials = computed(() => initialsFor(this.accountName()));
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      startWith(null),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );
  readonly crumbs = computed(() => this.crumbsForUrl(this.url()));
  readonly visibleGroups = computed(() => this.groupsForProfile());
  readonly businessSettingsVisible = computed(() =>
    this.visibleGroups().some((group) => group.items.some((item) => item.path === "/settings")),
  );
  readonly searchResults = computed(() => businessSearchResults({
    query: this.searchQuery(),
    groups: this.visibleGroups(),
    entitlements: businessEntitlements(this.profile()),
    navLabel: (item) => this.navLabel(item),
    tabLabel: (id) => this.tabLabel(id),
    discoverLabel: this.text("/__discover"),
    discoverUrl: this.discoverUrl(),
  }));

  constructor() {
    this.auth?.user$.pipe(takeUntilDestroyed()).subscribe((user) => {
      const name = user?.name ?? user?.email ?? "Business";
      this.accountName.set(name);
      this.accountDetail.set(user?.email ?? "");
    });
    this.navBadges.refreshRequested$.pipe(takeUntilDestroyed()).subscribe(() => {
      void this.loadBadges();
    });
    this.messagingRealtime.conversationUpdated$.pipe(takeUntilDestroyed()).subscribe(() => {
      void this.loadBadges();
    });
    this.operationsRealtime.pendingBookingsChanged$.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event.tenantId === this.profile()?.tenantId) {
        this.pendingBookings.set(event.pendingCount);
      }
    });
    this.operationsRealtime.tenantVerificationChanged$.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event.tenantId === this.profile()?.tenantId) {
        void this.refreshProfileSilently();
      }
    });
    this.activeLocation.selectionChanged$.pipe(takeUntilDestroyed()).subscribe(() => {
      void this.activeLocation.runOnBranchSwitch(() => this.loadBadges());
    });
    void this.loadProfile();
  }

  text(key: ChromeKey): string {
    return CHROME[this.locale()][key];
  }

  groupLabel(group: BusinessNavGroup): string {
    if (group.id === "today") return this.text("groupToday");
    if (group.id === "listing") return this.text("groupListing");
    return this.text("groupBusiness");
  }

  badgeCount(item: BusinessNavItem): number {
    if (item.badge === "bookings") return this.pendingBookings();
    if (item.badge === "messages") {
      if (!hasTenantEntitlement(businessEntitlements(this.profile()), "messaging")) {
        return 0;
      }
      return this.unreadMessages();
    }
    return 0;
  }

  navLabel(item: BusinessNavItem): string {
    const localized = CHROME[this.locale()][item.path as ChromeKey];
    return localized || item.label;
  }

  toggleSidebar(): void {
    this.setCollapsed(!this.collapsed());
  }

  collapseSidebar(): void {
    this.setCollapsed(true);
  }

  toggleTheme(): void {
    this.businessTheme.toggle();
  }

  setTheme(preference: ThemePreference): void {
    this.businessTheme.setPreference(preference);
  }

  setLocale(locale: LocaleId): void {
    this.localeService.setLocale(locale);
    this.settingsOpen.set(false);
  }

  toggleSettings(event: MouseEvent): void {
    event.stopPropagation();
    this.userOpen.set(false);
    this.searchOpen.set(false);
    this.settingsOpen.update((open) => !open);
  }

  closeSettingsMenu(): void {
    this.settingsOpen.set(false);
  }

  navigateSettings(path: string): void {
    this.closeSettingsMenu();
    void this.router.navigateByUrl(path);
  }

  toggleUser(event: MouseEvent): void {
    event.stopPropagation();
    this.settingsOpen.set(false);
    this.searchOpen.set(false);
    this.userOpen.update((open) => !open);
  }

  openSearch(): void {
    this.searchOpen.set(true);
    this.settingsOpen.set(false);
    this.userOpen.set(false);
  }

  onSearchInput(event: Event): void {
    const value = event.target instanceof HTMLInputElement ? event.target.value : "";
    this.searchQuery.set(value);
    this.searchIndex.set(0);
    this.openSearch();
  }

  onSearchKey(event: KeyboardEvent): void {
    const hits = this.searchResults();
    if (!hits.length) {
      return;
    }
    if (event.key === "ArrowDown") {
      event.preventDefault();
      this.searchIndex.update((index) => Math.min(hits.length - 1, index + 1));
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      this.searchIndex.update((index) => Math.max(0, index - 1));
    }
  }

  submitSearch(event: Event): void {
    event.preventDefault();
    const hit = this.searchResults()[this.searchIndex()] ?? this.searchResults()[0];
    if (hit) {
      this.openSearchHit(hit);
    }
  }

  openSearchHit(hit: SearchHit): void {
    this.searchOpen.set(false);
    this.searchQuery.set("");
    this.searchIndex.set(0);
    if (hit.external) {
      window.open(hit.path, "_blank", "noopener");
      return;
    }
    void this.router.navigateByUrl(hit.path);
  }

  logout(): void {
    this.userOpen.set(false);
    if (this.auth && this.auth0Mode) {
      this.auth.logout({
        logoutParams: { returnTo: window.location.origin },
      });
      return;
    }
    signOutPortalDev();
    void this.router.navigateByUrl("/setup");
  }

  discoverUrl(): string {
    return this.config.discoverWebUrl;
  }

  discoverNavHint(): string {
    return this.text("discoverHint");
  }

  @HostListener("document:click", ["$event"])
  closeMenus(event: MouseEvent): void {
    const target = event.target;
    if (!(target instanceof Element)) {
      this.settingsOpen.set(false);
      this.userOpen.set(false);
      this.searchOpen.set(false);
      return;
    }
    if (!target.closest("[data-menu-root='settings']")) {
      this.settingsOpen.set(false);
    }
    if (!target.closest("[data-menu-root='user']")) {
      this.userOpen.set(false);
    }
    if (!target.closest("[data-menu-root='search']")) {
      this.searchOpen.set(false);
    }
  }

  @HostListener("document:keydown", ["$event"])
  focusSearchFromChord(event: KeyboardEvent): void {
    if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== "k") {
      return;
    }
    event.preventDefault();
    this.searchField()?.nativeElement.focus();
    this.openSearch();
  }

  @HostListener("document:keydown.escape")
  closeMenusOnEscape(): void {
    this.settingsOpen.set(false);
    this.userOpen.set(false);
    this.searchOpen.set(false);
  }

  private crumbsForUrl(url: string): { label: string; path?: string }[] {
    const match = findBusinessNavItem(url) ?? findBusinessNavItem(BUSINESS_NAV[0].path);
    if (!match) {
      return [{ label: this.text("groupToday") }];
    }
    const crumbs = [
      { label: this.groupLabel(match.group) },
      { label: this.navLabel(match.item), path: match.item.tabs ? match.item.path : undefined },
    ];
    const tabId = activeBusinessTab(url, match.item.tabs);
    const tab = match.item.tabs?.find((entry) => entry.id === tabId);
    if (tab) {
      crumbs.push({ label: this.tabLabel(tab.id) });
    }
    return crumbs;
  }

  private tabLabel(id: string): string {
    const key: ChromeKey | null =
      id === "pending" ? "tabPending"
      : id === "upcoming" ? "tabUpcoming"
      : id === "past" ? "tabPast"
      : id === "overview" || id === "details" ? "tabOverview"
      : id === "business" ? "tabBusiness"
      : id === "reviews" ? "tabReviews"
      : id === "verification" ? "tabVerification"
      : id === "inbox" ? "tabInbox"
      : id === "whatsapp" ? "tabWhatsapp"
      : id === "links" ? "tabLinks"
      : id === "ledger" ? "tabLedger"
      : id === "bookings" ? "tabBookings"
      : id === "notifications" ? "tabNotifications"
      : id === "policies" ? "tabPolicies"
      : id === "messaging" ? "tabMessaging"
      : id === "walk-ins" ? "tabWalkIns"
      : null;
    return key ? this.text(key) : id;
  }

  private groupsForProfile(): BusinessNavGroup[] {
    const profile = this.profile();
    const fromSession = this.businessSession.permissions();
    const permissions =
      fromSession.length > 0
        ? fromSession
        : permissionsForRole(this.businessSession.permissionRole());
    const ctx = {
      profile,
      profileLoaded: this.profileLoaded(),
      permissions,
    };
    return businessNavigationGroups(ctx);
  }

  private async loadProfile(): Promise<void> {
    try {
      await this.businessSession.refresh();
      const profile = await this.businessApi.getTenantProfile();
      this.profile.set(profile);
      this.activeLocation.syncFromProfile(profile);
      await this.portalRealtime.syncForProfile(profile);
      if (profile?.businessName) {
        this.documentTitleStrategy.setAppTitle(profile.businessName);
        if (this.devMode) {
          this.accountName.set(profile.businessName);
        }
      }
      await this.loadBadges();
    } catch {
      this.profile.set(null);
      await this.portalRealtime.stop();
    } finally {
      this.profileLoaded.set(true);
    }
  }

  /** Realtime verification change: refresh nav/status chrome without resetting realtime or session. */
  private async refreshProfileSilently(): Promise<void> {
    try {
      const profile = await this.businessApi.getTenantProfile();
      if (profile) {
        this.profile.set(profile);
      }
    } catch {
      // Next navigation or manual refresh will reload.
    }
  }

  private async loadBadges(): Promise<void> {
    try {
      const profile = this.profile();
      const messaging = hasTenantEntitlement(businessEntitlements(profile), "messaging");
      const entitlements = businessEntitlements(profile);
      const locationQueryId = this.activeLocation.tenantApiLocationId(entitlements);
      const [bookingsPage, unread] = await this.businessApi.withAuthorizedClient((client) =>
        Promise.all([
          client
            .listTenantBookings({
              tab: "pending",
              page: 1,
              pageSize: 100,
              locationId: locationQueryId,
            })
            .catch(() => ({ items: [], totalCount: 0, page: 1, pageSize: 100 })),
          messaging
            ? client.getTenantMessageUnreadCount(locationQueryId).catch(() => 0)
            : Promise.resolve(0),
        ]),
      );
      this.pendingBookings.set(bookingsPage.items.filter((booking) => booking.status === 0).length);
      this.unreadMessages.set(unread);
    } catch {
      this.pendingBookings.set(0);
      this.unreadMessages.set(0);
    }
  }

  private setCollapsed(collapsed: boolean): void {
    this.collapsed.set(collapsed);
    try {
      localStorage.setItem(SIDEBAR_STORAGE_KEY, collapsed ? "1" : "0");
    } catch {
      // Preference is optional.
    }
  }
}
