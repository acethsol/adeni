import Link from "next/link";
import { getOptionalSession } from "@/lib/auth/session";
import { isAuth0Configured } from "@/lib/auth/config";
import { Button } from "@/components/ui/button";

export async function AuthNav() {
  if (!isAuth0Configured()) {
    return null;
  }

  const session = await getOptionalSession();

  if (!session) {
    return (
      <Button href="/auth/login" variant="secondary" size="sm">
        Log in
      </Button>
    );
  }

  return (
    <div className="flex items-center gap-2 sm:gap-3">
      <Link href="/my-bookings" className="hidden text-sm font-semibold text-accent hover:underline sm:inline">
        My bookings
      </Link>
      <span className="hidden text-sm text-muted md:inline">
        {session.name ?? session.email ?? "Signed in"}
      </span>
      <Button href="/auth/logout" variant="secondary" size="sm">
        Log out
      </Button>
    </div>
  );
}
