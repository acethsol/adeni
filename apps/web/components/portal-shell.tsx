import type { ReactNode } from "react";
import Link from "next/link";
import { PublicHeader } from "@/components/public-header";

type Props = {
  title: string;
  description: string;
  children: ReactNode;
};

export function PortalShell({ title, description, children }: Props) {
  return (
    <div className="min-h-screen bg-[#f6f8f6] text-[#1b4332]">
      <PublicHeader />
      <main className="mx-auto max-w-5xl px-6 py-16">
        <Link href="/" className="text-sm font-medium text-[#40916c]">
          ← Back to home
        </Link>
        <h1 className="mt-4 text-3xl font-bold">{title}</h1>
        <p className="mt-3 max-w-2xl text-[#1b4332]/80">{description}</p>
        <div className="mt-8">{children}</div>
      </main>
    </div>
  );
}
