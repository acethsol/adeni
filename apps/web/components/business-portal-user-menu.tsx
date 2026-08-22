"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { ChevronDown, ExternalLink, LogOut, UserCircle } from "lucide-react";
import { formatTenantStatus } from "@adeni/shared";
import { cn } from "@/lib/cn";

type Props = {
  businessName: string | null;
  status: number | null;
  primarySlug: string | null;
  sessionName: string | null;
  sessionEmail: string | null;
  mode: "auth0" | "dev";
};

export function BusinessPortalUserMenu({
  businessName,
  status,
  primarySlug,
  sessionName,
  sessionEmail,
  mode,
}: Props) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    function onPointerDown(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    function onKey(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }

    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const displayName = sessionName ?? businessName ?? "Business owner";
  const initial = displayName.trim().charAt(0).toUpperCase() || "A";
  const subtitle = sessionEmail ?? (businessName && businessName !== displayName ? businessName : null);

  return (
    <div className="relative" ref={containerRef}>
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-haspopup="menu"
        aria-expanded={open}
        className="flex items-center gap-2 rounded-full py-1 pl-1 pr-2 transition-colors hover:bg-subtle"
      >
        <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-accent/15 text-xs font-bold text-accent">
          {initial}
        </span>
        <span className="hidden text-left leading-tight sm:block">
          <span className="block max-w-[9rem] truncate text-sm font-semibold text-foreground">
            {displayName}
          </span>
          {subtitle ? (
            <span className="block max-w-[9rem] truncate text-xs text-muted">{subtitle}</span>
          ) : null}
        </span>
        <ChevronDown className={cn("h-3.5 w-3.5 shrink-0 text-muted transition-transform", open && "rotate-180")} aria-hidden />
      </button>

      {open ? (
        <div
          role="menu"
          className="adeni-toast-enter absolute right-0 top-full z-30 mt-2 w-64 overflow-hidden rounded-2xl border border-border bg-surface p-1.5 shadow-xl"
        >
          <div className="px-3 py-2.5">
            <p className="truncate text-sm font-semibold text-foreground">{businessName ?? "Your business"}</p>
            {status !== null ? (
              <p className="mt-0.5 text-xs text-muted">{formatTenantStatus(status)}</p>
            ) : null}
          </div>
          <div className="mx-1 border-t border-border" />
          <div className="p-1">
            {primarySlug ? (
              <a
                href={`/businesses/${primarySlug}`}
                target="_blank"
                rel="noopener noreferrer"
                role="menuitem"
                onClick={() => setOpen(false)}
                className="flex items-center gap-2.5 rounded-xl px-3 py-2 text-sm font-medium text-foreground transition-colors hover:bg-subtle"
              >
                <ExternalLink className="h-4 w-4 shrink-0 text-muted" aria-hidden />
                View public profile
              </a>
            ) : null}
            <Link
              href="/business/profile"
              role="menuitem"
              onClick={() => setOpen(false)}
              className="flex items-center gap-2.5 rounded-xl px-3 py-2 text-sm font-medium text-foreground transition-colors hover:bg-subtle"
            >
              <UserCircle className="h-4 w-4 shrink-0 text-muted" aria-hidden />
              Account settings
            </Link>
          </div>
          <div className="mx-1 border-t border-border" />
          <div className="p-1">
            {mode === "auth0" ? (
              <Link
                href="/auth/logout"
                role="menuitem"
                className="flex items-center gap-2.5 rounded-xl px-3 py-2 text-sm font-semibold text-destructive transition-colors hover:bg-destructive-bg"
              >
                <LogOut className="h-4 w-4 shrink-0" aria-hidden />
                Sign out
              </Link>
            ) : (
              <p className="px-3 py-2 text-xs text-muted">Local dev session — no sign out</p>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}
