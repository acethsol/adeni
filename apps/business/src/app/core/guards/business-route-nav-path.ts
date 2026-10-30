import type { ActivatedRouteSnapshot } from "@angular/router";

/** Full path segments for guard checks (e.g. `/retention/memberships`, not `memberships`). */
export function businessRouteNavPath(route: ActivatedRouteSnapshot): string {
  const segments: string[] = [];
  let current: ActivatedRouteSnapshot | undefined = route;
  while (current) {
    if (current.url.length > 0) {
      segments.unshift(...current.url.map((segment) => segment.path));
    }
    current = current.parent ?? undefined;
  }
  const joined = segments.join("/");
  return joined ? `/${joined}` : "/";
}

/** Retention child routes use path segments instead of `?tab=`. */
export function retentionTabIdFromPath(navPath: string): string | null {
  const match = /^\/retention\/(packages|memberships|loyalty)(?:\/|$)/.exec(navPath);
  return match?.[1] ?? null;
}
