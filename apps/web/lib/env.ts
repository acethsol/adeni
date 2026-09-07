/**
 * True when the Next.js app runs a production build (NODE_ENV=production).
 * Dev-auth fallbacks must never activate in this mode.
 */
export function isProductionDeployment(): boolean {
  return process.env.NODE_ENV === "production";
}

/**
 * Dev impersonation headers are allowed only in local non-production builds.
 */
export function isDevAuthAllowed(): boolean {
  return !isProductionDeployment();
}
