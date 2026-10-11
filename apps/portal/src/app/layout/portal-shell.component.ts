import { Component, computed, DestroyRef, ElementRef, HostListener, inject, Injector, signal, viewChild } from "@angular/core";
import { takeUntilDestroyed, toSignal } from "@angular/core/rxjs-interop";
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import type { AuthService } from "@auth0/auth0-angular";
import type { AdeniBrandSurface } from "@adeni/brand";
import {
  formatTenantStatus,
  hasCapability,
  hasPortalPermission,
  resolveCapabilities,
  type BusinessProfile,
  type LocaleId,
} from "@adeni/shared";
import {
  AdeniBrandLogoComponent,
  AdeniConfirmHostComponent,
  AdeniGlobalLoadingPanelComponent,
  AdeniLocaleService,
  AdeniToastHostComponent,
} from "@adeni/ui";
import { filter, map, startWith } from "rxjs";
import { ADENI_PORTAL_CONFIG, isAuth0Configured, isBusinessPortalDevMode } from "../core/adeni-config";
import { resolveAuthService } from "../core/auth0-rxjs";
import { signOutPortalDev } from "../core/portal-dev-session";
import {
  PORTAL_NAV,
  PORTAL_NAV_GROUPS,
  activePortalTab,
  findPortalNavItem,
  type PortalNavGroup,
  type PortalNavItem,
} from "../core/portal-nav";
import { BusinessApiService } from "../core/services/business-api.service";
import { PortalSessionService } from "../core/services/portal-session.service";
import { PendingBookingsBellComponent } from "../shared/pending-bookings-bell.component";
import { PortalNavIconComponent } from "./portal-nav-icon.component";

const SIDEBAR_STORAGE_KEY = "adeni.portal.sidebar.collapsed";
const THEME_STORAGE_KEY = "adeni.portal.theme";

type PortalTheme = "light" | "dark";
type ThemePreference = PortalTheme | "system";
type ChromeKey =
  | "search"
  | "appearance"
  | "language"
  | "light"
  | "dark"
  | "system"
  | "signOut"
  | "settings"
  | "jump"
  | "marketplace"
  | "groupToday"
  | "groupListing"
  | "groupBusiness"
  | "tabPending"
  | "tabUpcoming"
  | "tabPast"
  | "tabDetails"
  | "tabReviews"
  | "tabVerification"
  | "tabInbox"
  | "tabWhatsapp"
  | "tabLinks"
  | "tabLedger"
  | "/dashboard"
  | "/bookings"
  | "/quotes"
  | "/messages"
  | "/services"
  | "/staff"
  | "/locations"
  | "/availability"
  | "/profile"
  | "/public-page"
  | "/register"
  | "/payments"
  | "/plan";

const CHROME: Record<LocaleId, Record<ChromeKey, string>> = {
  en: {
    search: "Search",
    appearance: "Appearance",
    language: "Language",
    light: "Light",
    dark: "Dark",
    system: "Match system",
    signOut: "Sign out",
    settings: "Settings",
    jump: "Jump to",
    marketplace: "Marketplace",
    groupToday: "Today",
    groupListing: "Listing",
    groupBusiness: "Business",
    tabPending: "Pending",
    tabUpcoming: "Upcoming",
    tabPast: "Past",
    tabDetails: "Details",
    tabReviews: "Reviews",
    tabVerification: "Verification",
    tabInbox: "Inbox",
    tabWhatsapp: "WhatsApp",
    tabLinks: "Links",
    tabLedger: "Ledger",
    "/dashboard": "Overview",
    "/bookings": "Bookings",
    "/quotes": "Quotes",
    "/messages": "Messages",
    "/services": "Services",
    "/staff": "Staff",
    "/locations": "Locations",
    "/availability": "Hours",
    "/profile": "Profile",
    "/public-page": "Public page",
    "/register": "Register",
    "/payments": "Payments",
    "/plan": "Plan",
  },
  fr: {
    search: "Rechercher",
    appearance: "Apparence",
    language: "Langue",
    light: "Clair",
    dark: "Sombre",
    system: "Système",
    signOut: "Se déconnecter",
    settings: "Paramètres",
    jump: "Aller à",
    marketplace: "Place de marché",
    groupToday: "Aujourd'hui",
    groupListing: "Vitrine",
    groupBusiness: "Entreprise",
    tabPending: "En attente",
    tabUpcoming: "À venir",
    tabPast: "Passées",
    tabDetails: "Détails",
    tabReviews: "Avis",
    tabVerification: "Vérification",
    tabInbox: "Boîte",
    tabWhatsapp: "WhatsApp",
    tabLinks: "Liens",
    tabLedger: "Journal",
    "/dashboard": "Aperçu",
    "/bookings": "Réservations",
    "/quotes": "Devis",
    "/messages": "Messages",
    "/services": "Services",
    "/staff": "Équipe",
    "/locations": "Lieux",
    "/availability": "Horaires",
    "/profile": "Profil",
    "/public-page": "Page publique",
    "/register": "Inscription",
    "/payments": "Paiements",
    "/plan": "Forfait",
  },
};

type SearchHit = {
  id: string;
  label: string;
  path: string;
  icon: PortalNavItem["icon"];
  external?: boolean;
};

function readThemePreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === "light" || stored === "dark" || stored === "system") {
      return stored;
    }
  } catch {
    // Preference is optional.
  }
  return "system";
}

function resolveTheme(preference: ThemePreference): PortalTheme {
  if (preference === "light" || preference === "dark") {
    return preference;
  }
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

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
  selector: "app-portal-shell",
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    AdeniBrandLogoComponent,
    AdeniGlobalLoadingPanelComponent,
    AdeniToastHostComponent,
    AdeniConfirmHostComponent,
    PendingBookingsBellComponent,
    PortalNavIconComponent,
  ],
  templateUrl: "./portal-shell.component.html",
  styleUrl: "./portal-shell.component.scss",
})
export class PortalShellComponent {
  private readonly localeService = inject(AdeniLocaleService);
  readonly locales = this.localeService.locales;
  readonly locale = this.localeService.locale;
  readonly formatTenantStatus = formatTenantStatus;
  readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly injector = inject(Injector);
  private readonly router = inject(Router);
  private readonly businessApi = inject(BusinessApiService);
  private readonly portalSession = inject(PortalSessionService);
  private readonly destroyRef = inject(DestroyRef);
  readonly auth: AuthService | null = isAuth0Configured(this.config)
    ? resolveAuthService(this.injector)
    : null;

  readonly devMode = isBusinessPortalDevMode(this.config);
  readonly auth0Mode = isAuth0Configured(this.config);
  readonly collapsed = signal(readSidebarCollapsed());
  readonly themePreference = signal<ThemePreference>(readThemePreference());
  readonly theme = signal<PortalTheme>(resolveTheme(this.themePreference()));
  readonly logoSurface = computed<AdeniBrandSurface>(() => (this.theme() === "dark" ? "dark" : "light"));
  readonly settingsOpen = signal(false);
  readonly userOpen = signal(false);
  readonly searchQuery = signal("");
  readonly searchOpen = signal(false);
  readonly searchIndex = signal(0);
  private readonly searchField = viewChild<ElementRef<HTMLInputElement>>("searchField");
  readonly profile = signal<BusinessProfile | null>(null);
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
  readonly searchResults = computed<SearchHit[]>(() => {
    const query = this.searchQuery().trim().toLowerCase();
    const pages = this.visibleGroups()
      .flatMap((group) => group.items)
      .filter((item) => !query || this.navLabel(item).toLowerCase().includes(query) || item.label.toLowerCase().includes(query))
      .map((item) => ({
        id: item.path,
        label: this.navLabel(item),
        path: item.path,
        icon: item.icon,
      }));
    const marketplaceLabel = this.text("marketplace");
    const marketplace =
      !query || marketplaceLabel.toLowerCase().includes(query)
        ? [{ id: "marketplace", label: marketplaceLabel, path: this.discoverUrl(), icon: "launch" as const, external: true }]
        : [];
    return [...pages, ...marketplace].slice(0, 8);
  });

  constructor() {
    this.applyTheme(this.theme());
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const onSystemTheme = (): void => {
      if (this.themePreference() === "system") {
        this.applyTheme(resolveTheme("system"));
      }
    };
    media.addEventListener("change", onSystemTheme);
    this.destroyRef.onDestroy(() => media.removeEventListener("change", onSystemTheme));

    this.auth?.user$.pipe(takeUntilDestroyed()).subscribe((user) => {
      const name = user?.name ?? user?.email ?? "Business";
      this.accountName.set(name);
      this.accountDetail.set(user?.email ?? "");
    });
    void this.loadProfile();
  }

  text(key: ChromeKey): string {
    return CHROME[this.locale()][key];
  }

  groupLabel(group: PortalNavGroup): string {
    if (group.id === "today") return this.text("groupToday");
    if (group.id === "listing") return this.text("groupListing");
    return this.text("groupBusiness");
  }

  badgeCount(item: PortalNavItem): number {
    if (item.badge === "bookings") return this.pendingBookings();
    if (item.badge === "messages") return this.unreadMessages();
    return 0;
  }

  navLabel(item: PortalNavItem): string {
    return this.text(item.path as ChromeKey);
  }

  toggleSidebar(): void {
    this.setCollapsed(!this.collapsed());
  }

  collapseSidebar(): void {
    this.setCollapsed(true);
  }

  toggleTheme(): void {
    this.setTheme(this.theme() === "dark" ? "light" : "dark");
  }

  setTheme(preference: ThemePreference): void {
    this.themePreference.set(preference);
    this.applyTheme(resolveTheme(preference));
    try {
      localStorage.setItem(THEME_STORAGE_KEY, preference);
    } catch {
      // Preference is optional.
    }
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
    const match = findPortalNavItem(url) ?? findPortalNavItem(PORTAL_NAV[0].path);
    if (!match) {
      return [{ label: this.text("groupToday") }];
    }
    const crumbs = [
      { label: this.groupLabel(match.group) },
      { label: this.navLabel(match.item), path: match.item.tabs ? match.item.path : undefined },
    ];
    const tabId = activePortalTab(url, match.item.tabs);
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
      : id === "details" ? "tabDetails"
      : id === "reviews" ? "tabReviews"
      : id === "verification" ? "tabVerification"
      : id === "inbox" ? "tabInbox"
      : id === "whatsapp" ? "tabWhatsapp"
      : id === "links" ? "tabLinks"
      : id === "ledger" ? "tabLedger"
      : null;
    return key ? this.text(key) : id;
  }

  private groupsForProfile(): PortalNavGroup[] {
    const profile = this.profile();
    const capabilities = profile
      ? resolveCapabilities(profile.businessType, profile.categorySlug, profile.capabilities)
      : null;
    const permissions = this.portalSession.permissions();
    return PORTAL_NAV_GROUPS.map((group) => ({
      ...group,
      items: group.items.filter((item) => {
        if (item.unregisteredOnly) {
          return this.profileLoaded() && !profile;
        }
        if (item.permission && !hasPortalPermission(permissions, item.permission)) {
          return false;
        }
        if (
          item.excludeIfPermission &&
          hasPortalPermission(permissions, item.excludeIfPermission)
        ) {
          return false;
        }
        if (!capabilities) {
          return true;
        }
        return !item.capability || hasCapability(capabilities, item.capability);
      }),
    })).filter((group) => group.items.length > 0);
  }

  private applyTheme(theme: PortalTheme): void {
    this.theme.set(theme);
    document.documentElement.dataset["theme"] = theme;
  }

  private async loadProfile(): Promise<void> {
    try {
      await this.portalSession.refresh();
      const profile = await this.businessApi.getTenantProfile();
      this.profile.set(profile);
      if (profile?.businessName && this.devMode) {
        this.accountName.set(profile.businessName);
      }
      await this.loadBadges();
    } catch {
      this.profile.set(null);
    } finally {
      this.profileLoaded.set(true);
    }
  }

  private async loadBadges(): Promise<void> {
    try {
      const [bookings, unread] = await this.businessApi.withAuthorizedClient((client) =>
        Promise.all([
          client.getTenantBookings().catch(() => []),
          client.getTenantMessageUnreadCount().catch(() => 0),
        ]),
      );
      this.pendingBookings.set(bookings.filter((booking) => booking.status === 0).length);
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
