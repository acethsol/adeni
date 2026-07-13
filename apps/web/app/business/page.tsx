import Link from "next/link";
import {
  ArrowRight,
  CalendarDays,
  Clock3,
  MapPin,
  Scissors,
  ShieldCheck,
  Sparkles,
  UserCircle,
} from "lucide-react";
import { formatTenantStatus } from "@adeni/shared";
import { AuthSetupCallout } from "@/components/auth-setup-callout";
import { BusinessPortalCard } from "@/components/business-portal-card";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import { Button } from "@/components/ui/button";
import {
  canAccessBusinessPortal,
  requireBusinessPortalAccess,
} from "@/lib/business-access";
import { createBusinessApiClient } from "@/lib/business-api";

export default async function BusinessPortalPage() {
  if (!canAccessBusinessPortal()) {
    return (
      <BusinessPortalShell
        title="Grow with Adeni"
        description="Manage bookings, services, and your public profile — all in one place."
      >
        <AuthSetupCallout />
        <BusinessPortalCard className="mt-6 max-w-xl">
          <p className="text-sm text-muted">
            For local dev without Auth0, set{" "}
            <code className="text-xs">DEV_BUSINESS_AUTH0_SUB=auth0|local-business</code> in{" "}
            <code className="text-xs">.env.local</code> (linked to{" "}
            <code className="text-xs">lekki-cuts</code> in dev seed).
          </p>
        </BusinessPortalCard>
      </BusinessPortalShell>
    );
  }

  const access = await requireBusinessPortalAccess("/business");

  let profile = null;
  let bookingsCount = 0;
  let servicesCount = 0;
  let loadError: string | null = null;

  try {
    const client = await createBusinessApiClient();
    profile = await client.getTenantProfile();
    const [bookings, services] = await Promise.all([
      client.getTenantBookings(),
      client.getTenantServices(),
    ]);
    bookingsCount = bookings.filter((item) => item.status === 0).length;
    servicesCount = services.filter((item) => item.isActive).length;
  } catch {
    loadError =
      "Could not load business data. Ensure the API is running and your business account is linked.";
  }

  const primaryLocation = profile?.locations.find((item) => item.isPrimary) ?? profile?.locations[0];

  return (
    <BusinessPortalShell
      title="Overview"
      description="Your command center for bookings, services, and customer-facing profile."
      devMode={access.mode === "dev"}
      hasBusiness={Boolean(profile)}
      actions={
        profile && primaryLocation ? (
          <Button href={`/businesses/${primaryLocation.slug}`} variant="secondary">
            View public profile
          </Button>
        ) : undefined
      }
    >
      {access.session ? (
        <p className="mb-6 text-sm text-muted">
          Signed in as{" "}
          <span className="font-semibold text-foreground">
            {access.session.name ?? access.session.email ?? "business user"}
          </span>
        </p>
      ) : null}

      {loadError ? (
        <BusinessPortalCard>
          <p className="text-sm text-muted">{loadError}</p>
          <Button href="/business/register" className="mt-4">
            Register your business
          </Button>
        </BusinessPortalCard>
      ) : profile ? (
        <div className="space-y-8">
          <div className="grid gap-4 sm:grid-cols-3">
            <StatCard
              label="Status"
              value={formatTenantStatus(profile.status)}
              hint="Verification progress"
            />
            <StatCard
              label="Pending bookings"
              value={String(bookingsCount)}
              hint="Awaiting your response"
            />
            <StatCard
              label="Active services"
              value={String(servicesCount)}
              hint="Visible to customers"
            />
          </div>

          <BusinessPortalCard padding="lg" className="relative overflow-hidden">
            <div
              className="pointer-events-none absolute -left-10 top-0 h-40 w-40 rounded-full bg-accent/10 blur-3xl"
              aria-hidden
            />
            <div className="relative flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
              <div>
                <div className="flex items-center gap-2 text-sm font-semibold text-accent">
                  <Sparkles className="h-4 w-4" aria-hidden />
                  Your business
                </div>
                <h2 className="mt-2 text-2xl font-bold tracking-tight">{profile.businessName}</h2>
                <p className="mt-2 text-sm text-muted">
                  {profile.categorySlug.replace(/-/g, " ")} · {profile.phone}
                </p>
                {primaryLocation ? (
                  <p className="mt-1 text-sm text-muted">
                    Primary location: {primaryLocation.name} ({primaryLocation.slug})
                  </p>
                ) : null}
              </div>
              {bookingsCount > 0 ? (
                <Button href="/business/bookings" size="lg">
                  {bookingsCount} booking{bookingsCount === 1 ? "" : "s"} waiting
                  <ArrowRight className="ml-2 h-4 w-4" aria-hidden />
                </Button>
              ) : (
                <Button href="/business/services" variant="secondary" size="lg">
                  Add a service
                </Button>
              )}
            </div>
          </BusinessPortalCard>

          <div>
            <h3 className="text-sm font-bold uppercase tracking-widest text-accent">
              Quick actions
            </h3>
            <div className="mt-4 grid gap-4 sm:grid-cols-2">
              <QuickAction
                href="/business/bookings"
                icon={CalendarDays}
                title="Booking inbox"
                description="Confirm appointments and manage your schedule."
              />
              <QuickAction
                href="/business/services"
                icon={Scissors}
                title="Services & pricing"
                description="Update what customers can book."
              />
              <QuickAction
                href="/business/availability"
                icon={Clock3}
                title="Weekly hours"
                description="Set when you're open for bookings."
              />
              <QuickAction
                href="/business/locations"
                icon={MapPin}
                title="Locations"
                description="Branches, areas, and public profile URLs."
              />
              <QuickAction
                href="/business/profile"
                icon={UserCircle}
                title="Profile & verification"
                description="Photos, description, and trust badge."
              />
              <QuickAction
                href="/business/profile"
                icon={ShieldCheck}
                title="Get verified"
                description="Stand out with an Adeni verified badge."
              />
            </div>
          </div>
        </div>
      ) : null}
    </BusinessPortalShell>
  );
}

function StatCard({
  label,
  value,
  hint,
}: {
  label: string;
  value: string;
  hint: string;
}) {
  return (
    <BusinessPortalCard>
      <p className="text-xs font-bold uppercase tracking-wider text-accent">{label}</p>
      <p className="mt-2 text-3xl font-bold tracking-tight text-foreground">{value}</p>
      <p className="mt-1 text-xs text-muted">{hint}</p>
    </BusinessPortalCard>
  );
}

function QuickAction({
  href,
  icon: Icon,
  title,
  description,
}: {
  href: string;
  icon: typeof CalendarDays;
  title: string;
  description: string;
}) {
  return (
    <Link
      href={href}
      className="group flex gap-4 rounded-2xl border border-border bg-surface p-5 shadow-sm transition-all hover:border-accent/30 hover:shadow-md"
    >
      <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-subtle text-accent transition-colors group-hover:bg-accent/10">
        <Icon className="h-5 w-5" aria-hidden />
      </div>
      <div>
        <p className="font-semibold text-foreground">{title}</p>
        <p className="mt-1 text-sm leading-relaxed text-muted">{description}</p>
      </div>
    </Link>
  );
}
