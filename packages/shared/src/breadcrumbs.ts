/** One segment in a staff/customer portal breadcrumb trail. */
export type AdeniBreadcrumb = {
  label: string;
  /** Router path (no query). Omit on the current (leaf) segment. */
  path?: string;
};

/** Strip query/hash and trailing slashes from a router URL. */
export function breadcrumbPathname(url: string): string {
  const path = (url.split(/[?#]/)[0] ?? url).replace(/\/+$/, "");
  return path || "/";
}

export function mergeBreadcrumbTrails(
  base: readonly AdeniBreadcrumb[],
  extras: readonly AdeniBreadcrumb[],
): AdeniBreadcrumb[] {
  if (!extras.length) {
    return [...base];
  }
  return [...base, ...extras];
}
