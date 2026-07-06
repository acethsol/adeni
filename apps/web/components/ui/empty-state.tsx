import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/cn";

type Props = {
  title: string;
  description?: string;
  actionLabel?: string;
  actionHref?: string;
  onAction?: () => void;
  icon?: ReactNode;
  className?: string;
};

export function EmptyState({
  title,
  description,
  actionLabel,
  actionHref,
  onAction,
  icon,
  className,
}: Props) {
  return (
    <div
      className={cn(
        "flex flex-col items-center rounded-xl border border-border bg-surface px-6 py-10 text-center shadow-sm",
        className,
      )}
    >
      {icon ? (
        <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-accent/10 text-accent">
          {icon}
        </div>
      ) : (
        <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-accent/10 text-xl">
          ✦
        </div>
      )}
      <p className="text-lg font-semibold text-foreground">{title}</p>
      {description ? <p className="mt-2 max-w-sm text-sm text-muted">{description}</p> : null}
      {actionLabel && actionHref ? (
        <Button href={actionHref} className="mt-5">
          {actionLabel}
        </Button>
      ) : null}
      {actionLabel && onAction ? (
        <Button className="mt-5" onClick={onAction}>
          {actionLabel}
        </Button>
      ) : null}
    </div>
  );
}
