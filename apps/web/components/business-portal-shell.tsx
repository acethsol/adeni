import type { ReactNode } from "react";
import { PublicHeader } from "@/components/public-header";
import { BusinessPortalNav } from "@/components/business-portal-nav";
import { Callout } from "@/components/ui/callout";
import { PageHeader } from "@/components/ui/page-header";

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
    <div className="min-h-screen bg-background text-foreground">
      <PublicHeader />
      <main className="mx-auto max-w-5xl px-6 py-12">
        <PageHeader
          eyebrow="Business portal"
          title={title}
          description={description}
          backHref="/"
          backLabel="← Back to home"
        />

        {devMode ? (
          <Callout tone="success" className="mt-4">
            Local dev mode — using <code className="text-xs">DEV_BUSINESS_AUTH0_SUB</code> (linked to{" "}
            <code className="text-xs">lekki-cuts</code> in dev seed).
          </Callout>
        ) : null}

        <BusinessPortalNav />

        <div className="mt-8">{children}</div>
      </main>
    </div>
  );
}
