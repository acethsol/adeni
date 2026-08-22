"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { LogOut } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/cn";

type NavSession = {
  name: string | null;
  email: string | null;
};

type NavState = {
  loading: boolean;
  configured: boolean;
  session: NavSession | null;
};

type Props = {
  hideBookingsLink?: boolean;
  tone?: "light" | "dark";
  compact?: boolean;
};

export function AuthNavClient({ hideBookingsLink = false, tone = "light", compact = false }: Props = {}) {
  const [state, setState] = useState<NavState>({
    loading: true,
    configured: false,
    session: null,
  });

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const response = await fetch("/api/auth/nav", { cache: "no-store" });
        const data = (await response.json()) as NavState;

        if (!cancelled) {
          setState({
            loading: false,
            configured: data.configured,
            session: data.session,
          });
        }
      } catch {
        if (!cancelled) {
          setState({ loading: false, configured: false, session: null });
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, []);

  if (state.loading) {
    return <span className={cn("inline-block h-9", compact ? "w-9" : "w-20")} aria-hidden />;
  }

  if (!state.configured) {
    return null;
  }

  if (!state.session) {
    if (compact) {
      return (
        <Link
          href="/auth/login"
          title="Log in"
          className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-white/70 transition-colors hover:bg-white/10 hover:text-white"
        >
          <LogOut className="h-4 w-4 rotate-180" aria-hidden />
        </Link>
      );
    }

    return (
      <Button href="/auth/login" variant="secondary" size="sm">
        Log in
      </Button>
    );
  }

  if (compact) {
    return (
      <Link
        href="/auth/logout"
        title="Log out"
        className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-white/70 transition-colors hover:bg-white/10 hover:text-white"
      >
        <LogOut className="h-4 w-4" aria-hidden />
      </Link>
    );
  }

  return (
    <div className="flex items-center gap-2 sm:gap-3">
      {hideBookingsLink ? null : (
        <Link
          href="/my-bookings"
          className={cn(
            "hidden text-sm font-semibold hover:underline sm:inline",
            tone === "dark" ? "text-white" : "text-accent",
          )}
        >
          My bookings
        </Link>
      )}
      <span
        className={cn(
          "hidden truncate text-sm md:inline",
          tone === "dark" ? "text-white/60" : "text-muted",
        )}
      >
        {state.session.name ?? state.session.email ?? "Signed in"}
      </span>
      <Button href="/auth/logout" variant="secondary" size="sm">
        Log out
      </Button>
    </div>
  );
}
