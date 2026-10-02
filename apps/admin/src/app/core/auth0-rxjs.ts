import { firstValueFrom, type Observable } from "rxjs";

/** Auth0 Angular observables can resolve against a nested rxjs copy in npm workspaces. */
export function auth0FirstValueFrom<T>(source: unknown): Promise<T> {
  return firstValueFrom(source as Observable<T>);
}
