import Link from "next/link";
import { AuthNav } from "@/components/auth-nav";
import { canAccessMyBookings } from "@/lib/customer-access";

export function PublicHeader() {
  const showMyBookings = canAccessMyBookings();

  return (
    <header className="border-b border-[#1b4332]/10 bg-white">
      <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <Link href="/" className="text-xl font-bold tracking-tight text-[#1b4332]">
          Adeni
        </Link>
        <nav className="flex items-center gap-4 text-sm font-medium text-[#1b4332]">
          <Link href="/discover">Discover</Link>
          {showMyBookings ? <Link href="/my-bookings">My bookings</Link> : null}
          <Link href="/business">For business</Link>
          <Link href="/admin">Admin</Link>
          <AuthNav />
        </nav>
      </div>
    </header>
  );
}
