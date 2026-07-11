import Link from "next/link";
import { ChevronDown, MapPin } from "lucide-react";
import { cn } from "@/lib/cn";

type Props = {
  marketName: string;
  label: string;
  href?: string;
  className?: string;
  as?: "link" | "span";
};

export function MarketLocationBadge({
  marketName,
  label,
  href = "/discover",
  className,
  as = "link",
}: Props) {
  const content = (
    <>
      <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-accent/15 ring-1 ring-accent/20">
        <MapPin className="h-4 w-4 text-accent" aria-hidden />
      </span>
      <span className="min-w-0 text-left">
        <span className="block text-[10px] font-semibold uppercase tracking-[0.14em] text-muted sm:text-[11px]">
          {label}
        </span>
        <span className="block truncate text-sm font-semibold text-foreground sm:text-base">
          {marketName}
        </span>
      </span>
      <ChevronDown
        className="h-4 w-4 shrink-0 text-muted opacity-50 transition-opacity group-hover:opacity-100"
        aria-hidden
      />
    </>
  );

  const classNames = cn(
    "group inline-flex items-center gap-2.5 rounded-full border border-accent/20 bg-gradient-to-r from-accent/5 to-transparent px-3.5 py-2 shadow-sm transition-all hover:border-accent/35 hover:from-accent/10 hover:shadow-md sm:px-4 sm:py-2.5",
    className,
  );

  if (as === "span") {
    return (
      <span className={classNames} aria-label={`${label} ${marketName}`}>
        {content}
      </span>
    );
  }

  return (
    <Link
      href={href}
      className={classNames}
      aria-label={`${label} ${marketName}`}
    >
      {content}
    </Link>
  );
}
