import Link from "next/link";
import {
  ArrowRight,
  CalendarDays,
  CheckCircle2,
  ExternalLink,
  MapPin,
  Scissors,
  ShieldCheck,
} from "lucide-react";
import { formatTenantStatus } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { BusinessPortalCard } from "@/components/business-portal-card";

type Props = {
  businessName: string;
  categorySlug: string;
  status: number;
  primarySlug?: string | null;
  primaryLocationName?: string | null;
};

const nextSteps = [
  {
    icon: Scissors,
    title: "Add your services",
    description: "List what customers can book and set prices.",
    href: "/business/services",
  },
  {
    icon: CalendarDays,
    title: "Set your hours",
    description: "Open slots so bookings can flow in.",
    href: "/business/availability",
  },
  {
    icon: ShieldCheck,
    title: "Complete verification",
    description: "Build trust with a verified badge on your profile.",
    href: "/business/profile",
  },
] as const;

export function BusinessAlreadyRegistered({
  businessName,
  categorySlug,
  status,
  primarySlug,
  primaryLocationName,
}: Props) {
  const categoryLabel = categorySlug.replace(/-/g, " ");

  return (
    <div className="space-y-6">
      <BusinessPortalCard padding="lg" className="relative overflow-hidden">
        <div
          className="pointer-events-none absolute -right-16 -top-16 h-48 w-48 rounded-full bg-accent/10 blur-2xl"
          aria-hidden
        />
        <div className="relative flex flex-col gap-6 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex gap-4">
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-accent/15 text-accent">
              <CheckCircle2 className="h-7 w-7" aria-hidden />
            </div>
            <div>
              <p className="text-sm font-semibold text-accent">You&apos;re all set</p>
              <h2 className="mt-1 text-2xl font-bold tracking-tight text-foreground">
                {businessName}
              </h2>
              <p className="mt-2 text-sm text-muted">
                {categoryLabel} · Status:{" "}
                <span className="font-semibold text-foreground">
                  {formatTenantStatus(status)}
                </span>
              </p>
              {primaryLocationName && primarySlug ? (
                <p className="mt-1 flex items-center gap-1.5 text-sm text-muted">
                  <MapPin className="h-4 w-4 shrink-0 text-accent" aria-hidden />
                  {primaryLocationName}
                </p>
              ) : null}
            </div>
          </div>
          <div className="flex flex-wrap gap-3 sm:flex-col sm:items-stretch">
            <Button href="/business" size="lg">
              Open your portal
              <ArrowRight className="ml-2 h-4 w-4" aria-hidden />
            </Button>
            {primarySlug ? (
              <Button
                href={`/businesses/${primarySlug}`}
                variant="secondary"
                size="lg"
                className="gap-2"
              >
                View public profile
                <ExternalLink className="h-4 w-4" aria-hidden />
              </Button>
            ) : null}
          </div>
        </div>
      </BusinessPortalCard>

      <div>
        <h3 className="text-sm font-bold uppercase tracking-widest text-accent">
          Recommended next steps
        </h3>
        <div className="mt-4 grid gap-4 sm:grid-cols-3">
          {nextSteps.map((step) => (
            <Link
              key={step.href}
              href={step.href}
              className="group rounded-2xl border border-border bg-surface p-5 shadow-sm transition-all hover:border-accent/30 hover:shadow-md"
            >
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-subtle text-accent transition-colors group-hover:bg-accent/10">
                <step.icon className="h-5 w-5" aria-hidden />
              </div>
              <p className="mt-4 font-semibold text-foreground">{step.title}</p>
              <p className="mt-1 text-sm leading-relaxed text-muted">{step.description}</p>
              <span className="mt-4 inline-flex items-center gap-1 text-sm font-semibold text-accent">
                Continue
                <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
              </span>
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}
