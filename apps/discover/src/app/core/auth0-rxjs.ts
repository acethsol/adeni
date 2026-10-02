import type { Observable } from "rxjs";
import { firstValueFrom } from "rxjs";

/** Workspace hoists a duplicate rxjs; Auth0 types expect the nested copy. */
export function auth0FirstValueFrom<T>(source: Observable<T>): Promise<T> {
  return firstValueFrom(source);
}
