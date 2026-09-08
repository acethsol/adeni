"use client";

import { ChevronRight } from "lucide-react";
import { useBusinessPortalPageLabel } from "@/components/business-portal-nav";
import { BusinessPortalBell } from "@/components/business-portal-bell";
import { BusinessPortalMessagesBell } from "@/components/business-portal-messages-bell";
import { BusinessPortalUserMenu } from "@/components/business-portal-user-menu";

type Props = {
  businessName: string | null;
  status: number | null;
  primarySlug: string | null;
  sessionName: string | null;
  sessionEmail: string | null;
  mode: "auth0" | "dev";
};

export function BusinessPortalTopbar({
  businessName,
  status,
  primarySlug,
  sessionName,
  sessionEmail,
  mode,
}: Props) {
  const pageLabel = useBusinessPortalPageLabel();

  return (
    <header className="sticky top-0 z-20 hidden h-16 items-center justify-between gap-4 border-b border-border bg-surface/85 px-6 backdrop-blur-md lg:flex lg:px-10">
      <div className="flex items-center gap-1.5 text-sm font-semibold text-muted">
        <span>Business</span>
        {pageLabel ? (
          <>
            <ChevronRight className="h-3.5 w-3.5 text-border-strong" aria-hidden />
            <span className="text-foreground">{pageLabel}</span>
          </>
        ) : null}
      </div>

      <div className="flex items-center gap-1.5">
        <BusinessPortalBell />
        <BusinessPortalMessagesBell />
        <div className="mx-1 h-6 w-px bg-border" aria-hidden />
        <BusinessPortalUserMenu
          businessName={businessName}
          status={status}
          primarySlug={primarySlug}
          sessionName={sessionName}
          sessionEmail={sessionEmail}
          mode={mode}
        />
      </div>
    </header>
  );
}
