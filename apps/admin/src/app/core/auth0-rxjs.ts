import { Injector } from "@angular/core";
import { AuthService } from "@auth0/auth0-angular";
import { firstValueFrom, type Observable } from "rxjs";

/** Auth0 Angular observables can resolve against a nested rxjs copy in npm workspaces. */
export function auth0FirstValueFrom<T>(source: unknown): Promise<T> {
  return firstValueFrom(source as Observable<T>);
}

/** See portal `auth0-rxjs.ts` — avoid optional inject of root AuthService without provideAuth0. */
export function resolveAuthService(injector: Injector): AuthService | null {
  try {
    return injector.get(AuthService);
  } catch {
    return null;
  }
}
