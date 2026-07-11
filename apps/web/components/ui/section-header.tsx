import type { ReactNode } from "react";
import Link from "next/link";
import { cn } from "@/lib/cn";

type Props = {
  title: string;
  description?: string;
  action?: ReactNode;
  className?: string;
};

export function SectionHeader({ title, description, action, className }: Props) {
  return (
    <div className={cn("flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between", className)}>
      <div>
        <h2 className="text-xl font-semibold tracking-tight text-foreground sm:text-2xl">{title}</h2>
        {description ? <p className="mt-1 max-w-2xl text-sm text-muted sm:text-base">{description}</p> : null}
      </div>
      {action ? <div className="shrink-0">{action}</div> : null}
    </div>
  );
}

type ChipProps = {
  href: string;
  label: string;
  icon?: string;
};

export function CategoryChip({ href, label, icon }: ChipProps) {
  return (
    <Link
      href={href}
      className="inline-flex items-center gap-1.5 rounded-full border border-border bg-surface px-4 py-2 text-sm font-semibold text-foreground shadow-sm transition-colors hover:border-accent/30 hover:bg-subtle"
    >
      {icon ? <span aria-hidden>{icon}</span> : null}
      {label}
    </Link>
  );
}
