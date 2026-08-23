import Link from "next/link";
import type { ReactNode } from "react";
import {
  ArrowRight,
  CalendarDays,
  Clock3,
  MapPin,
  Scissors,
  ShieldCheck,
  Sparkles,
  Star,
  UserCircle,
  Wallet,
} from "lucide-react";
import { formatTenantStatus } from "@adeni/shared";
import { BusinessOverviewCharts } from "@/components/business-overview-charts";
import { BusinessPortalCard } from "@/components/business-portal-card";
import { BusinessPortalShell } from "@/components/business-portal-shell";
import { SubscriptionUsageMeter } from "@/components/subscription-usage-meter";
import { Button } from "@/components/ui/button";
import { createBusinessApiClient } from "@/lib/business-api";
import type { SubscriptionUsage } from "@adeni/shared";

const DAY_LABELS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

function startOfWeek(date: Date): Date {
  const result = new Date(date);
  const day = result.getDay();
  const diff = (day === 0 ? -6 : 1) - day;
  result.setDate(result.getDate() + diff);
  result.setHours(0, 0, 0, 0);
  return result;
}

function formatCurrency(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat(undefined, {
      style: "currency",
      currency,
      maximumFractionDigits: 0,
    }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(0)}`;
  }
}

export default async function BusinessPortalPage() {
  let profile = null;
  let bookingsCount = 0;
  let servicesCount = 0;
  let charts: {
    weekly: { label: string; count: number; isToday: boolean }[];
    statusBreakdown: { status: number; count: number }[];
    totalBookings: number;
  } | null = null;
  let revenueLabel: string | null = null;
  let rating: { avg: number | null; count: number } | null = null;
  let subscriptionUsage: SubscriptionUsage | null = null;
  let advancedAnalytics: {
    funnel: { label: string; count: number; color: string }[];
    repeatCustomerRate: number | null;
    serviceMix: { label: string; count: number; percent: number }[];
  } | null = null;
  let loadError: string | null = null;

  try {
    const client = await createBusinessApiClient();
    profile = await client.getTenantProfile();
    const [bookings, services, usage] = await Promise.all([
      client.getTenantBookings(),
      client.getTenantServices(),
      client.getTenantSubscriptionUsage(),
    ]);
    subscriptionUsage = usage;
    bookingsCount = bookings.filter((item) => item.status === 0).length;
    servicesCount = services.filter((item) => item.isActive).length;

    const weekStart = startOfWeek(new Date());
    const weekly = DAY_LABELS.map((label, index) => {
      const dayDate = new Date(weekStart);
      dayDate.setDate(weekStart.getDate() + index);
      const count = bookings.filter((booking) => {
        if (booking.status === 2 || booking.status === 3) return false;
        const startDate = new Date(booking.startAt);
        return startDate.toDateString() === dayDate.toDateString();
      }).length;
      return { label, count, isToday: dayDate.toDateString() === new Date().toDateString() };
    });

    const statusBreakdown = [0, 1, 2, 3].map((status) => ({
      status,
      count: bookings.filter((item) => item.status === status).length,
    }));

    charts = { weekly, statusBreakdown, totalBookings: bookings.length };

    const serviceById = new Map(services.map((service) => [service.id, service]));

    if (usage.entitlements.analytics) {
      const pending = bookings.filter((b) => b.status === 0).length;
      const confirmed = bookings.filter((b) => b.status === 1).length;
      const rejected = bookings.filter((b) => b.status === 2).length;
      const cancelled = bookings.filter((b) => b.status === 3).length;

      const customerCounts = new Map<string, number>();
      for (const booking of bookings) {
        customerCounts.set(booking.customerId, (customerCounts.get(booking.customerId) ?? 0) + 1);
      }
      const uniqueCustomers = customerCounts.size;
      const repeatCustomers = [...customerCounts.values()].filter((count) => count > 1).length;

      const serviceCounts = new Map<string, number>();
      for (const booking of bookings) {
        const name = serviceById.get(booking.serviceOfferingId)?.name ?? booking.serviceName;
        serviceCounts.set(name, (serviceCounts.get(name) ?? 0) + 1);
      }
      const totalForMix = bookings.length || 1;
      const serviceMix = [...serviceCounts.entries()]
        .sort((a, b) => b[1] - a[1])
        .slice(0, 5)
        .map(([label, count]) => ({
          label,
          count,
          percent: Math.round((count / totalForMix) * 100),
        }));

      advancedAnalytics = {
        funnel: [
          { label: "Pending", count: pending, color: "bg-amber-400" },
          { label: "Confirmed", count: confirmed, color: "bg-accent" },
          { label: "Rejected", count: rejected, color: "bg-destructive/70" },
          { label: "Cancelled", count: cancelled, color: "bg-muted-foreground/50" },
        ],
        repeatCustomerRate: uniqueCustomers >= 2 ? repeatCustomers / uniqueCustomers : null,
        serviceMix,
      };
    }

    const confirmedRevenue = bookings
      .filter((item) => item.status === 1)
      .reduce((sum, item) => sum + (serviceById.get(item.serviceOfferingId)?.priceAmount ?? 0), 0);
    const currency = services[0]?.currency ?? "NGN";
    revenueLabel = formatCurrency(confirmedRevenue, currency);

    const primaryLocation = profile.locations.find((item) => item.isPrimary) ?? profile.locations[0];
    if (primaryLocation) {
      try {
        const publicProfile = await client.getBusinessProfile(primaryLocation.slug);
        rating = { avg: publicProfile.ratingAvg ?? null, count: publicProfile.reviewCount ?? 0 };
      } catch {
        rating = null;
      }
    }
  } catch {
    loadError =
      "Could not load business data. Ensure the API is running and your business account is linked.";
  }

  const primaryLocation = profile?.locations.find((item) => item.isPrimary) ?? profile?.locations[0];

  return (
    <BusinessPortalShell
      title="Overview"
      description="Your command center for bookings, services, and customer-facing profile."
      actions={
        profile && primaryLocation ? (
          <Button href={`/businesses/${primaryLocation.slug}`} variant="secondary" target="_blank">
            View public profile
          </Button>
        ) : undefined
      }
    >
      {loadError ? (
        <BusinessPortalCard>
          <p className="text-sm text-muted">{loadError}</p>
          <Button href="/business/register" className="mt-4">
            Register your business
          </Button>
        </BusinessPortalCard>
      ) : profile ? (
        <div className="space-y-8">
          {subscriptionUsage ? (
            <SubscriptionUsageMeter usage={subscriptionUsage} compact />
          ) : null}

          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
            <StatCard label="Status" value={formatTenantStatus(profile.status)} hint="Verification progress" />
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
            <StatCard
              label="Confirmed revenue"
              value={revenueLabel ?? "—"}
              hint="From confirmed bookings"
              icon={<Wallet className="h-4 w-4" aria-hidden />}
            />
            <StatCard
              label="Rating"
              value={rating?.avg ? rating.avg.toFixed(1) : "New"}
              hint={rating?.count ? `${rating.count} review${rating.count === 1 ? "" : "s"}` : "No reviews yet"}
              icon={<Star className="h-4 w-4" aria-hidden />}
            />
          </div>

          {charts ? (
            <BusinessOverviewCharts
              weekly={charts.weekly}
              statusBreakdown={charts.statusBreakdown}
              totalBookings={charts.totalBookings}
              showAdvancedAnalytics={subscriptionUsage?.entitlements.analytics ?? false}
              funnel={advancedAnalytics?.funnel}
              repeatCustomerRate={advancedAnalytics?.repeatCustomerRate}
              serviceMix={advancedAnalytics?.serviceMix}
            />
          ) : null}

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
            <div className="mt-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
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
                title="Profile & reviews"
                description="Photos, description, and customer feedback."
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
  icon,
}: {
  label: string;
  value: string;
  hint: string;
  icon?: ReactNode;
}) {
  return (
    <BusinessPortalCard>
      <p className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-accent">
        {icon}
        {label}
      </p>
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
