/**
 * Same-origin allowlist for the post-login return path (`appState.target`).
 *
 * Never trust a query-supplied redirect: only relative paths under known prefixes are accepted,
 * so an attacker-crafted login link can never bounce a customer to another origin.
 */
const SAFE_TARGET_CHARSET = /^\/(?!\/)[A-Za-z0-9/_\-.~?=&%#]*$/;

const ALLOWED_PREFIXES = ["/app", "/businesses", "/pay", "/discover"] as const;

export function isSafeAuthReturnTarget(target: unknown): target is string {
  if (typeof target !== "string" || target.length === 0 || target.length > 512) {
    return false;
  }
  if (target.includes("\\") || !SAFE_TARGET_CHARSET.test(target)) {
    return false;
  }

  const path = target.split(/[?#]/)[0] ?? "";
  if (path.split("/").some((segment) => segment === "..")) {
    return false;
  }
  if (path === "/") {
    return true;
  }

  return ALLOWED_PREFIXES.some((prefix) => path === prefix || path.startsWith(`${prefix}/`));
}

/** Returns `target` when safe, otherwise `fallback`. */
export function resolveAuthReturnTarget(target: unknown, fallback: string): string {
  return isSafeAuthReturnTarget(target) ? target : fallback;
}
