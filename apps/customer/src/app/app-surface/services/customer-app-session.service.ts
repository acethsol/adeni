import { computed, DestroyRef, inject, Injectable, Injector, PLATFORM_ID, signal } from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import {
  ADENI_DISCOVER_CONFIG,
  isAuth0Configured,
  isDiscoverCustomerDevMode,
} from "../../core/adeni-config";
import { resolveAuthService } from "../../core/auth0-rxjs";
import { CustomerApiService } from "../../core/services/customer-api.service";

type UserLike = { name?: string | null; email?: string | null; nickname?: string | null } | null | undefined;
type UserSource = { subscribe(next: (user: UserLike) => void): { unsubscribe(): void } };

/**
 * Signed-in state + identity for the customer app shell.
 * Identity comes from Auth0 ID-token claims and lives in memory only — never persisted, logged or sent to analytics.
 */
@Injectable({ providedIn: "root" })
export class CustomerAppSessionService {
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly api = inject(CustomerApiService);
  private readonly injector = inject(Injector);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private started = false;

  readonly auth0Mode = isAuth0Configured(this.config);
  readonly devCustomerMode = isDiscoverCustomerDevMode(this.config);
  readonly signedIn = signal(this.devCustomerMode);
  readonly displayName = signal<string | null>(null);
  readonly email = signal<string | null>(null);

  /** First name for greetings; `null` when unknown. */
  readonly firstName = computed(() => this.displayName()?.trim().split(/\s+/)[0] || null);

  readonly initials = computed(() => {
    const parts = (this.displayName() ?? "").trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return this.devCustomerMode ? "DC" : "";
    }
    if (parts.length === 1) {
      return parts[0]!.slice(0, 2).toUpperCase();
    }
    return `${parts[0]![0] ?? ""}${parts[parts.length - 1]![0] ?? ""}`.toUpperCase();
  });

  /** Idempotent; call from shells that need identity. */
  start(): void {
    if (this.started || !this.browser) {
      return;
    }
    this.started = true;

    if (this.devCustomerMode) {
      this.signedIn.set(true);
      return;
    }
    if (!this.auth0Mode) {
      return;
    }

    void this.api.isLoggedIn().then((value) => this.signedIn.set(value));
    const auth = resolveAuthService(this.injector);
    if (!auth) {
      return;
    }
    const subscription = (auth.user$ as unknown as UserSource).subscribe((user) => {
      this.displayName.set(user?.name?.trim() || user?.nickname?.trim() || null);
      this.email.set(user?.email?.trim() || null);
    });
    this.injector.get(DestroyRef).onDestroy(() => subscription.unsubscribe());
  }

  logout(): void {
    void this.api.logout();
  }

  login(returnPath: string): void {
    void this.api.login(returnPath);
  }
}
