/** Public business portal marketing entry (discover “For businesses”). */
export function resolveBusinessPortalWelcomeUrl(portalAppUrl: string): string | null {
  const base = portalAppUrl.trim().replace(/\/$/, "");
  return base ? `${base}/welcome` : null;
}

/** Post-auth business registration route on the portal. */
export function resolveBusinessPortalRegisterUrl(portalAppUrl: string): string | null {
  const base = portalAppUrl.trim().replace(/\/$/, "");
  return base ? `${base}/register` : null;
}
