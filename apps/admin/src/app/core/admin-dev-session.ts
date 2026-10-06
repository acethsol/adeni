const DEV_SIGNED_OUT_KEY = "adeni.admin.dev.signed-out";

export function isAdminDevSignedOut(): boolean {
  try {
    return sessionStorage.getItem(DEV_SIGNED_OUT_KEY) === "1";
  } catch {
    return false;
  }
}

export function signOutAdminDev(): void {
  try {
    sessionStorage.setItem(DEV_SIGNED_OUT_KEY, "1");
  } catch {
    // Session storage can throw in private mode.
  }
}

export function signInAdminDev(): void {
  try {
    sessionStorage.removeItem(DEV_SIGNED_OUT_KEY);
  } catch {
    // Session storage can throw in private mode.
  }
}
