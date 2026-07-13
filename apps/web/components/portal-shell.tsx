import type { ReactNode } from "react";
import { PublicHeader } from "@/components/public-header";
import { PageHeader } from "@/components/ui/page-header";

type Props = {
  title: string;
  description: string;
  children: ReactNode;
};

export function PortalShell({ title, description, children }: Props) {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <PublicHeader />
      <main id="main-content" className="mx-auto max-w-5xl px-6 py-16">
        <PageHeader title={title} description={description} backHref="/" backLabel="Back to home" />
        <div className="mt-8">{children}</div>
      </main>
    </div>
  );
}
