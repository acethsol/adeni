import Link from "next/link";
import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { MyBookingsList } from "@/components/my-bookings-list";
import { PublicHeader } from "@/components/public-header";
import {
  canAccessMyBookings,
  hasMyBookingsSession,
} from "@/lib/customer-access";
import { isCustomerDevMode } from "@/lib/customer-api";

export default async function MyBookingsPage() {
  if (!canAccessMyBookings()) {
    return (
      <>
        <PublicHeader />
        <main className="mx-auto max-w-3xl px-6 py-10">
          <h1 className="text-3xl font-bold tracking-tight text-[#1b4332]">
            My bookings
          </h1>
          <p className="mt-2 text-[#1b4332]/70">
            Sign in to view appointments you have booked.
          </p>
          <div className="mt-8">
            <AuthSetupCallout />
          </div>
        </main>
      </>
    );
  }

  const hasSession = await hasMyBookingsSession();

  return (
    <>
      <PublicHeader />
      <main className="mx-auto max-w-3xl px-6 py-10">
        <h1 className="text-3xl font-bold tracking-tight text-[#1b4332]">
          My bookings
        </h1>
        <p className="mt-2 text-[#1b4332]/70">
          Appointments you have booked on Adeni.
        </p>

        {isCustomerDevMode() ? (
          <p className="mt-4 rounded-lg border border-[#40916c]/20 bg-[#f6f8f6] px-4 py-3 text-sm text-[#1b4332]/80">
            Local dev mode — using <code>DEV_CUSTOMER_AUTH0_SUB</code>.
          </p>
        ) : null}

        {!hasSession ? (
          <div className="mt-8 rounded-xl border border-[#1b4332]/10 bg-white p-8 text-center shadow-sm">
            <p className="font-medium text-[#1b4332]">Sign in to continue</p>
            <p className="mt-2 text-sm text-[#1b4332]/70">
              Your booking history is tied to your Adeni account.
            </p>
            <Link
              href="/auth/login?returnTo=/my-bookings"
              className="mt-5 inline-block rounded-full bg-[#1b4332] px-5 py-2.5 text-sm font-medium text-white hover:opacity-90"
            >
              Log in
            </Link>
          </div>
        ) : (
          <div className="mt-8">
            <MyBookingsList />
          </div>
        )}
      </main>
    </>
  );
}
