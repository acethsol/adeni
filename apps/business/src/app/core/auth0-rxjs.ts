import { Injector } from "@angular/core";
import { AuthService } from "@auth0/auth0-angular";
import { firstValueFrom, type Observable } from "rxjs";

/** Auth0 Angular observables can resolve against a nested rxjs copy in npm workspaces. */
export function auth0FirstValueFrom<T>(source: unknown): Promise<T> {
  return firstValueFrom(source as Observable<T>);
}

/**
 * Lazy AuthService lookup after `provideAuth0()` is registered.
 * Do not use `inject(AuthService, { optional: true })` — AuthService is `providedIn: 'root'`
 * and will still instantiate without `auth0.client` (NG0203 / missing provider in dev).
 */
export function resolveAuthService(injector: Injector): AuthService | null {
  try {
    return injector.get(AuthService);
  } catch {
    return null;
  }
}
