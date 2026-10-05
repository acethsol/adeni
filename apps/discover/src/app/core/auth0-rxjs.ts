import { Injector } from "@angular/core";
import { AuthService } from "@auth0/auth0-angular";
import type { Observable } from "rxjs";
import { firstValueFrom } from "rxjs";

/** Workspace hoists a duplicate rxjs; Auth0 types expect the nested copy. */
export function auth0FirstValueFrom<T>(source: Observable<T>): Promise<T> {
  return firstValueFrom(source);
}

export function resolveAuthService(injector: Injector): AuthService | null {
  try {
    return injector.get(AuthService);
  } catch {
    return null;
  }
}
