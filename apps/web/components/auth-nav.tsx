import Link from "next/link";
import { getOptionalSession } from "@/lib/auth/session";
import { isAuth0Configured } from "@/lib/auth/config";

export async function AuthNav() {
  if (!isAuth0Configured()) {
    return null;
  }

  const session = await getOptionalSession();

  if (!session) {
    return (
      <Link
        href="/auth/login"
        className="rounded-full border border-[#1b4332]/20 px-4 py-1.5 text-sm font-medium"
      >
        Log in
      </Link>
    );
  }

  return (
    <div className="flex items-center gap-3">
      <span className="hidden text-sm text-[#1b4332]/70 sm:inline">
        {session.name ?? session.email ?? "Signed in"}
      </span>
      <Link
        href="/auth/logout"
        className="rounded-full border border-[#1b4332]/20 px-4 py-1.5 text-sm font-medium"
      >
        Log out
      </Link>
    </div>
  );
}
