import { BOOKING_STATUS_LABELS } from "@adeni/shared";
import { BusinessPortalCard } from "@/components/business-portal-card";
import { cn } from "@/lib/cn";

type DayCount = { label: string; count: number; isToday: boolean };

type StatusBreakdown = { status: number; count: number };

type Props = {
  weekly: DayCount[];
  statusBreakdown: StatusBreakdown[];
  totalBookings: number;
};

const STATUS_BAR_COLOR: Record<number, string> = {
  0: "bg-amber-400",
  1: "bg-accent",
  2: "bg-destructive/70",
  3: "bg-muted-foreground/40",
};

export function BusinessOverviewCharts({ weekly, statusBreakdown, totalBookings }: Props) {
  const maxCount = Math.max(1, ...weekly.map((day) => day.count));

  return (
    <div className="grid gap-4 lg:grid-cols-5">
      <BusinessPortalCard className="lg:col-span-3">
        <h3 className="text-sm font-bold uppercase tracking-widest text-accent">Bookings this week</h3>
        <p className="mt-1 text-xs text-muted">Appointments scheduled per day, Monday to Sunday.</p>
        <div className="mt-6 flex h-40 items-end gap-2 sm:gap-3">
          {weekly.map((day) => (
            <div key={day.label} className="flex flex-1 flex-col items-center gap-2">
              <div className="flex h-32 w-full items-end justify-center">
                <div
                  className={cn(
                    "w-full max-w-8 rounded-t-md transition-all",
                    day.isToday ? "bg-accent" : "bg-accent/30",
                    day.count === 0 && "min-h-[3px]",
                  )}
                  style={{ height: `${Math.max(day.count > 0 ? 10 : 3, (day.count / maxCount) * 100)}%` }}
                  title={`${day.count} booking${day.count === 1 ? "" : "s"}`}
                />
              </div>
              <p className={cn("text-xs font-semibold", day.isToday ? "text-accent" : "text-muted-foreground")}>
                {day.label}
              </p>
              <p className="text-[11px] text-muted-foreground">{day.count}</p>
            </div>
          ))}
        </div>
      </BusinessPortalCard>

      <BusinessPortalCard className="lg:col-span-2">
        <h3 className="text-sm font-bold uppercase tracking-widest text-accent">Booking mix</h3>
        <p className="mt-1 text-xs text-muted">All-time status breakdown.</p>

        {totalBookings === 0 ? (
          <p className="mt-8 text-center text-sm text-muted">No bookings yet.</p>
        ) : (
          <>
            <div className="mt-6 flex h-3 w-full overflow-hidden rounded-full bg-subtle">
              {statusBreakdown.map((item) =>
                item.count > 0 ? (
                  <div
                    key={item.status}
                    className={STATUS_BAR_COLOR[item.status]}
                    style={{ width: `${(item.count / totalBookings) * 100}%` }}
                    title={`${BOOKING_STATUS_LABELS[item.status]}: ${item.count}`}
                  />
                ) : null,
              )}
            </div>
            <ul className="mt-4 space-y-2 text-sm">
              {statusBreakdown.map((item) => (
                <li key={item.status} className="flex items-center justify-between gap-2">
                  <span className="flex items-center gap-2 text-muted">
                    <span className={cn("h-2.5 w-2.5 shrink-0 rounded-full", STATUS_BAR_COLOR[item.status])} aria-hidden />
                    {BOOKING_STATUS_LABELS[item.status]}
                  </span>
                  <span className="font-semibold text-foreground">{item.count}</span>
                </li>
              ))}
            </ul>
          </>
        )}
      </BusinessPortalCard>
    </div>
  );
}
