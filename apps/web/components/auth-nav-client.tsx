"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";

type NavSession = {
  name: string | null;
  email: string | null;
};

type NavState = {
  loading: boolean;
  configured: boolean;
  session: NavSession | null;
};

export function AuthNavClient() {
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
    return <span className="inline-block h-9 w-20" aria-hidden />;
  }

  if (!state.configured) {
    return null;
  }

  if (!state.session) {
    return (
      <Button href="/auth/login" variant="secondary" size="sm">
        Log in
      </Button>
    );
  }

  return (
    <div className="flex items-center gap-2 sm:gap-3">
      <Link
        href="/my-bookings"
        className="hidden text-sm font-semibold text-accent hover:underline sm:inline"
      >
        My bookings
      </Link>
      <span className="hidden text-sm text-muted md:inline">
        {state.session.name ?? state.session.email ?? "Signed in"}
      </span>
      <Button href="/auth/logout" variant="secondary" size="sm">
        Log out
      </Button>
    </div>
  );
}
