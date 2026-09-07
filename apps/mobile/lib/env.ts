/**
 * Dev Auth0 subs (EXPO_PUBLIC_DEV_*) are bundled into the client — only allow in dev builds.
 */
export function isDevAuthAllowed(): boolean {
  return __DEV__;
}

export function getDevCustomerAuth0Sub(): string | undefined {
  if (!isDevAuthAllowed()) {
    return undefined;
  }

  return process.env.EXPO_PUBLIC_DEV_CUSTOMER_AUTH0_SUB?.trim() || undefined;
}

export function getDevBusinessAuth0Sub(): string | undefined {
  if (!isDevAuthAllowed()) {
    return undefined;
  }

  return process.env.EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB?.trim() || undefined;
}

export function getWebBaseUrl(): string {
  return process.env.EXPO_PUBLIC_WEB_BASE_URL?.trim() || "https://adeni.io";
}
