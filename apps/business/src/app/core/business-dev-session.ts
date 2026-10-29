const DEV_SIGNED_OUT_KEY = "adeni.portal.dev.signed-out";

export function isPortalDevSignedOut(): boolean {
  try {
    return sessionStorage.getItem(DEV_SIGNED_OUT_KEY) === "1";
  } catch {
    return false;
  }
}

export function signOutPortalDev(): void {
  try {
    sessionStorage.setItem(DEV_SIGNED_OUT_KEY, "1");
  } catch {
    // Session storage can throw in private mode.
  }
}

export function signInPortalDev(): void {
  try {
    sessionStorage.removeItem(DEV_SIGNED_OUT_KEY);
  } catch {
    // Session storage can throw in private mode.
  }
}
