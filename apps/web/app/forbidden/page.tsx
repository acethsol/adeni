import Link from "next/link";
import { PublicHeader } from "@/components/public-header";

export default function ForbiddenPage() {
  return (
    <div className="min-h-screen bg-[#f6f8f6] text-[#1b4332]">
      <PublicHeader />
      <main className="mx-auto max-w-lg px-6 py-24 text-center">
        <p className="text-sm font-semibold uppercase tracking-widest text-[#40916c]">
          403
        </p>
        <h1 className="mt-3 text-3xl font-bold">Access denied</h1>
        <p className="mt-4 text-[#1b4332]/80">
          Your account does not have permission to view this portal. Sign in with a
          business or admin account that includes the required role.
        </p>
        <div className="mt-8 flex flex-wrap justify-center gap-3">
          <Link
            href="/"
            className="rounded-full border border-[#1b4332]/20 px-6 py-3 text-sm font-semibold"
          >
            Go home
          </Link>
          <Link
            href="/auth/logout"
            className="rounded-full bg-[#1b4332] px-6 py-3 text-sm font-semibold text-white"
          >
            Switch account
          </Link>
        </div>
      </main>
    </div>
  );
}
