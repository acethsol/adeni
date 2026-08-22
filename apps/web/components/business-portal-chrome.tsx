"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { ChevronLeft, ChevronRight, Compass, Menu, X } from "lucide-react";
import { AuthNavClient } from "@/components/auth-nav-client";
import { BusinessPortalBell } from "@/components/business-portal-bell";
import { BusinessPortalNavLinks, useBusinessPortalPageLabel } from "@/components/business-portal-nav";
import { useBusinessSidebar } from "@/contexts/business-sidebar-context";
import { cn } from "@/lib/cn";
import { useSidebarTooltip } from "@/lib/use-sidebar-tooltip";

type Props = {
  hasBusiness?: boolean;
  capabilities?: readonly string[];
};

function BrandMark({ collapsed = false }: { collapsed?: boolean }) {
  return (
    <Link
      href="/business"
      className={cn("flex items-center gap-2.5", collapsed && "justify-center")}
    >
      <span className="relative flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white/15 text-sm font-bold text-white">
        A
        <span
          className="absolute -inset-1 -z-10 rounded-2xl bg-accent/40 opacity-0 blur-md transition-opacity group-hover:opacity-100"
          aria-hidden
        />
      </span>
      {collapsed ? null : (
        <div className="leading-tight">
          <p className="text-sm font-bold text-white">Adeni</p>
          <p className="text-[11px] font-semibold uppercase tracking-wider text-white/55">Business</p>
        </div>
      )}
    </Link>
  );
}

export function BusinessPortalChrome({ hasBusiness = true, capabilities }: Props) {
  const [open, setOpen] = useState(false);
  const pathname = usePathname();
  const pageLabel = useBusinessPortalPageLabel();
  const { collapsed, toggle } = useBusinessSidebar();
  const marketplaceTooltip = useSidebarTooltip<HTMLAnchorElement>("View marketplace", collapsed);

  useEffect(() => {
    setOpen(false);
  }, [pathname]);

  useEffect(() => {
    if (!open) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    window.addEventListener("keydown", onKey);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <>
      <header className="sticky top-0 z-40 flex items-center justify-between gap-3 border-b border-border bg-surface/95 px-4 py-3 backdrop-blur-md lg:hidden">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => setOpen(true)}
            aria-label="Open business menu"
            className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-foreground transition-colors hover:bg-subtle"
          >
            <Menu className="h-5 w-5" aria-hidden />
          </button>
          <span className="text-sm font-bold tracking-tight text-foreground">
            {pageLabel ?? "Business"}
          </span>
        </div>
        <div className="flex items-center gap-1">
          <BusinessPortalBell />
          <Link
            href="/discover"
            className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-muted transition-colors hover:bg-subtle hover:text-foreground"
            aria-label="View Adeni marketplace"
          >
            <Compass className="h-4 w-4" aria-hidden />
          </Link>
        </div>
      </header>

      {open ? (
        <div className="fixed inset-0 z-50 lg:hidden">
          <button
            type="button"
            aria-label="Close menu"
            className="absolute inset-0 bg-black/40"
            onClick={() => setOpen(false)}
          />
          <div className="relative flex h-full w-72 max-w-[80vw] flex-col bg-primary p-4 shadow-xl">
            <div className="flex items-center justify-between gap-2 px-1 pb-5">
              <BrandMark />
              <button
                type="button"
                onClick={() => setOpen(false)}
                aria-label="Close menu"
                className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-white/70 transition-colors hover:bg-white/10 hover:text-white"
              >
                <X className="h-4 w-4" aria-hidden />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto">
              <BusinessPortalNavLinks showRegister={!hasBusiness} onNavigate={() => setOpen(false)} capabilities={capabilities} />
            </div>
            <div className="space-y-3 border-t border-white/10 pt-4">
              <Link
                href="/discover"
                onClick={() => setOpen(false)}
                className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold text-white/65 transition-colors hover:bg-white/8 hover:text-white"
              >
                <Compass className="h-4 w-4 shrink-0 text-white/40" aria-hidden />
                View marketplace
              </Link>
              <div className="px-1">
                <AuthNavClient hideBookingsLink tone="dark" />
              </div>
            </div>
          </div>
        </div>
      ) : null}

      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-30 hidden flex-col bg-primary transition-[width] duration-200 lg:flex",
          collapsed ? "w-20" : "w-64",
        )}
      >
        <div className="pointer-events-none absolute inset-0 overflow-hidden">
          <div
            className="absolute -left-24 -top-24 h-64 w-64 rounded-full bg-accent/20 blur-3xl"
            aria-hidden
          />
        </div>

        <div className={cn("relative px-5 pb-5 pt-6 group", collapsed && "px-0")}>
          <BrandMark collapsed={collapsed} />
        </div>

        <button
          type="button"
          onClick={toggle}
          title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
          aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
          className="absolute -right-3 top-8 z-40 flex h-6 w-6 items-center justify-center rounded-full border border-border bg-surface text-muted shadow-md transition-colors hover:border-accent/50 hover:text-accent"
        >
          {collapsed ? <ChevronRight className="h-3.5 w-3.5" aria-hidden /> : <ChevronLeft className="h-3.5 w-3.5" aria-hidden />}
        </button>

        <div className={cn("relative flex-1 overflow-x-hidden overflow-y-auto px-3", collapsed && "px-2")}>
          <BusinessPortalNavLinks showRegister={!hasBusiness} collapsed={collapsed} capabilities={capabilities} />
        </div>

        <div className={cn("relative space-y-1 border-t border-white/10 px-3 py-4", collapsed && "px-2")}>
          <Link
            ref={marketplaceTooltip.ref}
            href="/discover"
            onMouseEnter={marketplaceTooltip.onMouseEnter}
            onMouseLeave={marketplaceTooltip.onMouseLeave}
            onFocus={marketplaceTooltip.onFocus}
            onBlur={marketplaceTooltip.onBlur}
            className={cn(
              "group relative flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold text-white/65 transition-colors hover:bg-white/8 hover:text-white",
              collapsed && "justify-center px-0",
            )}
          >
            <Compass className="h-4 w-4 shrink-0 text-white/40" aria-hidden />
            {collapsed ? null : "View marketplace"}
          </Link>
          {marketplaceTooltip.tooltip}
          <div className={cn("px-1", collapsed && "flex justify-center px-0")}>
            <AuthNavClient hideBookingsLink tone="dark" compact={collapsed} />
          </div>
        </div>
      </aside>
    </>
  );
}
