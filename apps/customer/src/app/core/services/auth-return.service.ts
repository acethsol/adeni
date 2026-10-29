import { inject, Injectable, Injector, PLATFORM_ID } from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { Router } from "@angular/router";
import { ADENI_DISCOVER_CONFIG, isAuth0Configured } from "../adeni-config";
import { resolveAuthService } from "../auth0-rxjs";
import { isSafeAuthReturnTarget, resolveAuthReturnTarget } from "../auth-return-target";

type AppStateLike = { target?: unknown } | null | undefined;
type AppStateSource = { subscribe(next: (state: AppStateLike) => void): { unsubscribe(): void } };

/**
 * Honors `appState.target` after the Auth0 redirect callback.
 *
 * `redirect_uri` stays the site origin (no Auth0 tenant change); `CustomerApiService.login(returnPath)`
 * stores the intended path in `appState`, and this service navigates there once the callback completes.
 * Injecting `AuthService` at startup (browser only) also guarantees the callback is handled even when
 * the landing page never touches auth itself.
 */
@Injectable({ providedIn: "root" })
export class AuthReturnService {
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly injector = inject(Injector);
  private readonly router = inject(Router);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private started = false;

  readonly isSafeTarget = isSafeAuthReturnTarget;

  resolveTarget(target: unknown, fallback: string): string {
    return resolveAuthReturnTarget(target, fallback);
  }

  start(): void {
    if (this.started || !this.browser || !isAuth0Configured(this.config)) {
      return;
    }

    const auth = resolveAuthService(this.injector);
    if (!auth) {
      return;
    }

    this.started = true;
    (auth.appState$ as unknown as AppStateSource).subscribe((state) => {
      const target = state?.target;
      if (target === undefined) {
        return;
      }
      // Unsafe targets are dropped silently — the user simply stays where Auth0 returned them.
      if (isSafeAuthReturnTarget(target)) {
        void this.router.navigateByUrl(target, { replaceUrl: true });
      }
    });
  }
}
