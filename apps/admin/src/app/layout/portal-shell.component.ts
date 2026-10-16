import { Component, computed, DestroyRef, ElementRef, HostListener, inject, Injector, signal, viewChild } from "@angular/core";
import { takeUntilDestroyed, toSignal } from "@angular/core/rxjs-interop";
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import type { AuthService } from "@auth0/auth0-angular";
import type { AdeniBrandSurface } from "@adeni/brand";
import { type LocaleId } from "@adeni/shared";
import {
  AdeniBrandLogoComponent,
  AdeniConfirmHostComponent,
  AdeniGlobalLoadingPanelComponent,
  AdeniLocaleService,
  AdeniToastHostComponent,
} from "@adeni/ui";
import { filter, map, startWith } from "rxjs";
import {
  ADMIN_NAV,
  ADMIN_NAV_GROUPS,
  findAdminNavItem,
  type AdminNavGroup,
  type AdminNavIconName,
  type AdminNavItem,
} from "../core/admin-nav";
import { signOutAdminDev } from "../core/admin-dev-session";
import { ADENI_ADMIN_CONFIG, isAdminPortalDevMode, isAuth0Configured } from "../core/adeni-config";
import { resolveAuthService } from "../core/auth0-rxjs";
import { AdminThemeService, type ThemePreference } from "../core/admin-theme.service";
import { AdminApiService } from "../core/services/admin-api.service";
import { AdminIconTooltipDirective } from "../shared/admin-icon-tooltip.directive";
import { AdminNavIconComponent } from "./admin-nav-icon.component";

const SIDEBAR_STORAGE_KEY = "adeni.admin.sidebar.collapsed";

type ChromeKey =
  | "search"
  | "appearance"
  | "language"
  | "light"
  | "dark"
  | "system"
  | "signOut"
  | "settings"
  | "customers"
  | "jump"
  | "groupOperations"
  | "groupPlatform"
  | "groupCompliance"
  | "/dashboard"
  | "/pending"
  | "/trust-badges"
  | "/reviews"
  | "/markets"
  | "/businesses"
  | "/insights"
  | "/customers";

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
    customers: "Search customers",
    jump: "Jump to",
    groupOperations: "Operations",
    groupPlatform: "Platform",
    groupCompliance: "Compliance",
    "/dashboard": "Dashboard",
    "/pending": "Pending verifications",
    "/trust-badges": "Trust badges",
    "/reviews": "Reviews",
    "/markets": "Markets",
    "/businesses": "Businesses",
    "/insights": "Subscription insights",
    "/customers": "Customers",
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
    customers: "Rechercher des clients",
    jump: "Aller à",
    groupOperations: "Opérations",
    groupPlatform: "Plateforme",
    groupCompliance: "Conformité",
    "/dashboard": "Tableau de bord",
    "/pending": "Vérifications en attente",
    "/trust-badges": "Badges de confiance",
    "/reviews": "Avis",
    "/markets": "Marchés",
    "/businesses": "Établissements",
    "/insights": "Abonnements",
    "/customers": "Clients",
  },
};

type SearchHit = {
  id: string;
  label: string;
  path: string;
  icon: AdminNavIconName;
  hint?: string;
  email?: string;
  query?: string;
};

function matchesQuery(query: string, parts: Array<string | null | undefined>): boolean {
  return parts
    .filter((part): part is string => Boolean(part))
    .join(" ")
    .toLowerCase()
    .includes(query);
}

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
    // Storage can throw in private mode.
  }

  return window.matchMedia("(max-width: 900px)").matches;
}

@Component({
  selector: "app-admin-shell",
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    AdeniBrandLogoComponent,
    AdeniGlobalLoadingPanelComponent,
    AdeniToastHostComponent,
    AdeniConfirmHostComponent,
    AdminNavIconComponent,
    AdminIconTooltipDirective,
  ],
  templateUrl: "./portal-shell.component.html",
  styleUrl: "./portal-shell.component.scss",
})
export class AdminShellComponent {
  readonly nav = ADMIN_NAV;
  readonly navGroups = ADMIN_NAV_GROUPS;
  readonly config = inject(ADENI_ADMIN_CONFIG);
  private readonly injector = inject(Injector);
  private readonly router = inject(Router);
  private readonly api = inject(AdminApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly localeService = inject(AdeniLocaleService);
  private readonly adminTheme = inject(AdminThemeService);
  readonly locales = this.localeService.locales;
  readonly locale = this.localeService.locale;
  readonly auth: AuthService | null = isAuth0Configured(this.config)
    ? resolveAuthService(this.injector)
    : null;

  readonly devMode = isAdminPortalDevMode(this.config);
  readonly auth0Mode = isAuth0Configured(this.config);
  readonly collapsed = signal(readSidebarCollapsed());
  readonly themePreference = this.adminTheme.preference;
  readonly theme = this.adminTheme.theme;
  readonly logoSurface = computed<AdeniBrandSurface>(() => (this.theme() === "dark" ? "dark" : "light"));
  readonly settingsOpen = signal(false);
  readonly userOpen = signal(false);
  readonly searchQuery = signal("");
  readonly searchOpen = signal(false);
  readonly searchIndex = signal(0);
  private readonly searchField = viewChild<ElementRef<HTMLInputElement>>("searchField");
  private readonly entityHits = signal<SearchHit[]>([]);
  private readonly customerHits = signal<SearchHit[]>([]);
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  readonly accountName = signal(this.devMode ? "Local admin" : "Admin");
  readonly accountDetail = signal(this.devMode ? this.config.devAdminAuth0Sub : "");
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
  readonly queueCount = signal<number | null>(null);
  readonly pendingBadgeCount = signal<number | null>(null);
  readonly queueLabel = computed(() => {
    const count = this.queueCount();
    if (!count) {
      return "Verification queue";
    }
    return count === 1 ? "1 business awaiting verification" : `${count} businesses awaiting verification`;
  });
  readonly searchResults = computed<SearchHit[]>(() => {
    const query = this.searchQuery().trim().toLowerCase();
    const pages = this.nav
      .filter((item) => {
        if (!query) {
          return true;
        }
        return matchesQuery(query, [this.navLabel(item), item.label]);
      })
      .map((item) => ({
        id: item.path,
        label: this.navLabel(item),
        path: item.path,
        icon: item.icon,
      }));
    if (!query) {
      return pages;
    }

    const people = this.customerHits();
    const entities = this.entityHits();
    const customerSearch =
      query.length >= 2 && people.length === 0
        ? [
            {
              id: `customers:${this.searchQuery().trim()}`,
              label: `${this.text("customers")} · ${this.searchQuery().trim()}`,
              path: "/customers",
              icon: "privacy" as const,
              query: this.searchQuery().trim(),
            },
          ]
        : [];

    return [...entities, ...people, ...customerSearch, ...pages].slice(0, 8);
  });

  constructor() {
    this.destroyRef.onDestroy(() => {
      if (this.searchTimer) {
        clearTimeout(this.searchTimer);
      }
    });

    this.auth?.user$.pipe(takeUntilDestroyed()).subscribe((user) => {
      const name = user?.name ?? user?.email ?? "Admin";
      this.accountName.set(name);
      this.accountDetail.set(user?.email ?? "");
    });
    void this.loadSearchCatalog();
  }

  text(key: ChromeKey): string {
    return CHROME[this.locale()][key];
  }

  navLabel(item: AdminNavItem): string {
    return this.text(item.path as ChromeKey);
  }

  groupLabel(group: AdminNavGroup): string {
    if (group.id === "operations") {
      return this.text("groupOperations");
    }
    if (group.id === "platform") {
      return this.text("groupPlatform");
    }
    return this.text("groupCompliance");
  }

  badgeCount(item: AdminNavItem): number {
    if (item.badge === "pending") {
      return this.queueCount() ?? 0;
    }
    if (item.badge === "pendingBadges") {
      return this.pendingBadgeCount() ?? 0;
    }
    return 0;
  }

  toggleSidebar(): void {
    this.setCollapsed(!this.collapsed());
  }

  collapseSidebar(): void {
    this.setCollapsed(true);
  }

  toggleTheme(): void {
    this.adminTheme.toggle();
  }

  setTheme(preference: ThemePreference): void {
    this.adminTheme.setPreference(preference);
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
    this.customerHits.set([]);
    this.entityHits.set([]);
    this.openSearch();
    this.scheduleRemoteSearch(value.trim());
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
    this.customerHits.set([]);
    if (hit.path === "/customers" && hit.query) {
      void this.router.navigate(["/customers"], { queryParams: { q: hit.query } });
      return;
    }
    if (hit.query) {
      void this.router.navigate([hit.path], { queryParams: { q: hit.query } });
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
    signOutAdminDev();
    void this.router.navigateByUrl("/setup");
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
    const match = findAdminNavItem(url) ?? findAdminNavItem(ADMIN_NAV[0].path);
    if (!match) {
      return [{ label: this.text("groupOperations") }, { label: this.text("/dashboard") }];
    }
    return [
      { label: this.groupLabel(match.group) },
      { label: this.navLabel(match.item), path: match.item.path },
    ];
  }

  private scheduleRemoteSearch(query: string): void {
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
    if (query.length < 2) {
      return;
    }
    this.searchTimer = setTimeout(() => {
      void this.loadRemoteSearchHits(query);
    }, 250);
  }

  private async loadRemoteSearchHits(query: string): Promise<void> {
    try {
      const [customers, businesses, pending, markets] = await this.api.withAuthorizedClient((client) =>
        Promise.all([
          client.searchAdminCustomers(query, { page: 1, pageSize: 5 }),
          client.listAdminBusinesses({ q: query, page: 1, pageSize: 5 }),
          client.listPendingBusinesses({ q: query, page: 1, pageSize: 5 }),
          client.listAdminMarkets({ q: query, page: 1, pageSize: 5 }),
        ]),
      );
      if (this.searchQuery().trim() !== query) {
        return;
      }
      this.customerHits.set(
        customers.items.map((item) => ({
          id: `customer:${item.id}`,
          label: item.name || item.email || item.auth0Sub,
          hint: item.email ?? undefined,
          path: "/customers",
          icon: "privacy" as const,
          query: item.email ?? query,
        })),
      );
      this.entityHits.set([
        ...businesses.items.map((item) => ({
          id: `business:${item.id}`,
          label: item.name,
          hint: item.slug,
          path: "/businesses",
          icon: "businesses" as const,
          query: item.name,
        })),
        ...pending.items.map((item) => ({
          id: `pending:${item.id}`,
          label: item.name,
          hint: item.marketId,
          path: "/pending",
          icon: "verifications" as const,
          query: item.name,
        })),
        ...markets.items.map((item) => ({
          id: `market:${item.id}`,
          label: item.name,
          hint: item.id,
          path: "/markets",
          icon: "markets" as const,
          query: item.name,
        })),
      ]);
    } catch {
      if (this.searchQuery().trim() === query) {
        this.customerHits.set([]);
        this.entityHits.set([]);
      }
    }
  }

  private async loadSearchCatalog(): Promise<void> {
    const [pendingPage, pendingBadges] = await Promise.all([
      this.api
        .withAuthorizedClient((client) => client.listPendingBusinesses({ page: 1, pageSize: 1 }))
        .catch(() => null),
      this.api
        .withAuthorizedClient((client) => client.listAdminPendingBadges({ page: 1, pageSize: 1 }))
        .catch(() => null),
    ]);
    if (pendingPage) {
      this.queueCount.set(pendingPage.totalCount);
    }
    if (pendingBadges) {
      this.pendingBadgeCount.set(pendingBadges.totalCount);
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
