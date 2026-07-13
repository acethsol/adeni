import type { ReactNode } from "react";
import { Briefcase } from "lucide-react";
import { publicContainerClass, publicHeroBandClass } from "@/lib/layout-classes";
import { cn } from "@/lib/cn";
import { LoadingSpinner } from "@/components/ui/loading-spinner";
import { Skeleton, SkeletonCard, SkeletonList } from "@/components/ui/skeleton";

type PanelProps = {
  message?: string;
  variant?: "overlay" | "inline" | "card";
  className?: string;
};

export function LoadingPanel({
  message = "Loading…",
  variant = "inline",
  className,
}: PanelProps) {
  if (variant === "overlay") {
    return (
      <div
        className="fixed inset-0 z-[200] flex items-center justify-center bg-primary/15 px-6 backdrop-blur-[2px]"
        role="status"
        aria-live="polite"
        aria-busy="true"
      >
        <div className="w-full max-w-sm rounded-2xl border border-border bg-surface p-8 text-center shadow-lg">
          <LoadingSpinner size="lg" className="mx-auto" label={message} />
          <p className="mt-5 text-sm font-medium text-foreground">{message}</p>
          <p className="mt-1 text-xs text-muted">This usually takes a moment.</p>
        </div>
      </div>
    );
  }

  if (variant === "card") {
    return (
      <div
        className={cn(
          "flex flex-col items-center justify-center rounded-2xl border border-border bg-surface px-8 py-12 text-center shadow-sm",
          className,
        )}
        role="status"
        aria-live="polite"
        aria-busy="true"
      >
        <LoadingSpinner size="lg" label={message} />
        <p className="mt-4 text-sm font-medium text-foreground">{message}</p>
      </div>
    );
  }

  return (
    <div
      className={cn("flex items-center justify-center gap-3 py-10", className)}
      role="status"
      aria-live="polite"
      aria-busy="true"
    >
      <LoadingSpinner label={message} />
      <p className="text-sm font-medium text-muted">{message}</p>
    </div>
  );
}

type RouteShellVariant = "minimal" | "public" | "business" | "admin";

export function RouteLoadingShell({ variant = "minimal" }: { variant?: RouteShellVariant }) {
  if (variant === "business") {
    return <BusinessRouteLoadingShell />;
  }

  if (variant === "public") {
    return <PublicRouteLoadingShell />;
  }

  if (variant === "admin") {
    return <AdminRouteLoadingShell />;
  }

  return (
    <div className="flex min-h-[50vh] items-center justify-center px-6">
      <LoadingPanel message="Loading page…" />
    </div>
  );
}

function PublicRouteLoadingShell() {
  return (
    <div className="min-h-screen bg-background">
      <div className="border-b border-border px-6 py-4 lg:px-8">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4">
          <Skeleton className="h-8 w-28" />
          <Skeleton className="h-10 w-64 rounded-full" />
        </div>
      </div>

      <div className={cn(publicContainerClass, "py-12 lg:py-16")}>
        <Skeleton className="h-10 w-2/3 max-w-lg" />
        <Skeleton className="mt-4 h-5 w-full max-w-xl" />
        <Skeleton className="mt-2 h-5 w-4/5 max-w-lg" />

        <div className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, index) => (
            <div
              key={index}
              className="h-[280px] animate-pulse rounded-2xl border border-border bg-subtle"
            />
          ))}
        </div>
      </div>
    </div>
  );
}

function BusinessRouteLoadingShell() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="border-b border-border px-6 py-4 lg:px-8">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4">
          <Skeleton className="h-8 w-28" />
          <Skeleton className="h-10 w-40 rounded-full" />
        </div>
      </div>

      <section className={cn(publicHeroBandClass, "border-b")}>
        <div className={cn(publicContainerClass, "py-10 lg:py-12")}>
          <div className="flex gap-4">
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-primary/10">
              <Briefcase className="h-7 w-7 text-primary/30" aria-hidden />
            </div>
            <div className="flex-1 space-y-3">
              <Skeleton className="h-3 w-28" />
              <Skeleton className="h-9 w-64 max-w-full" />
              <Skeleton className="h-5 w-full max-w-2xl" />
            </div>
          </div>
        </div>
      </section>

      <div className={cn(publicContainerClass, "py-10 lg:py-12")}>
        <div className="grid gap-8 lg:grid-cols-[220px_minmax(0,1fr)] lg:gap-12">
          <aside className="space-y-2">
            {Array.from({ length: 6 }).map((_, index) => (
              <Skeleton key={index} className="h-10 w-full rounded-xl" />
            ))}
          </aside>
          <div>
            <SkeletonList count={3} />
          </div>
        </div>
      </div>
    </div>
  );
}

function AdminRouteLoadingShell() {
  return (
    <div className={cn(publicContainerClass, "py-10")}>
      <Skeleton className="h-9 w-48" />
      <Skeleton className="mt-3 h-5 w-96 max-w-full" />
      <div className="mt-8 grid gap-6 lg:grid-cols-2">
        <SkeletonCard lines={4} />
        <SkeletonCard lines={4} />
      </div>
    </div>
  );
}

export function ActionLoadingOverlay({
  message,
  visible,
}: {
  message: string;
  visible: boolean;
}) {
  if (!visible) {
    return null;
  }

  return <LoadingPanel message={message} variant="overlay" />;
}
