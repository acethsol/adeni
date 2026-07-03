import Link from "next/link";
import type { ReactNode } from "react";
import { PublicHeader } from "@/components/public-header";
import { BusinessPortalNav } from "@/components/business-portal-nav";

type Props = {
  title: string;
  description: string;
  devMode?: boolean;
  children: ReactNode;
};

export function BusinessPortalShell({
  title,
  description,
  devMode = false,
  children,
}: Props) {
  return (
    <div className="min-h-screen bg-[#f6f8f6] text-[#1b4332]">
      <PublicHeader />
      <main className="mx-auto max-w-5xl px-6 py-12">
        <Link href="/" className="text-sm font-medium text-[#40916c]">
          ← Back to home
        </Link>

        <div className="mt-4 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="text-xs font-bold uppercase tracking-wider text-[#40916c]">
              Business portal
            </p>
            <h1 className="mt-1 text-3xl font-bold">{title}</h1>
            <p className="mt-2 max-w-2xl text-[#1b4332]/80">{description}</p>
          </div>
        </div>

        {devMode ? (
          <p className="mt-4 rounded-lg border border-[#40916c]/20 bg-white px-4 py-3 text-sm text-[#1b4332]/70">
            Local dev mode — using <code className="text-xs">DEV_BUSINESS_AUTH0_SUB</code>{" "}
            (linked to <code className="text-xs">lekki-cuts</code> in dev seed).
          </p>
        ) : null}

        <BusinessPortalNav />

        <div className="mt-8">{children}</div>
      </main>
    </div>
  );
}
