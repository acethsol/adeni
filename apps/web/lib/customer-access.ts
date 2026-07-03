import { getOptionalSession } from "./auth/session";
import { isAuth0Configured } from "./auth/config";
import { isCustomerDevMode } from "./customer-api";

export function canAccessMyBookings(): boolean {
  return isAuth0Configured() || isCustomerDevMode();
}

export async function hasMyBookingsSession(): Promise<boolean> {
  if (isCustomerDevMode()) {
    return true;
  }

  if (!isAuth0Configured()) {
    return false;
  }

  const session = await getOptionalSession();
  return Boolean(session);
}
