import type { ReactNode } from "react";
import { BackLink } from "@/components/ui/back-link";
import { cn } from "@/lib/cn";

type Props = {
  eyebrow?: string;
  title: string;
  description?: string;
  backHref?: string;
  backLabel?: string;
  actions?: ReactNode;
  className?: string;
};

export function PageHeader({
  eyebrow,
  title,
  description,
  backHref,
  backLabel = "Back",
  actions,
  className,
}: Props) {
  return (
    <header className={cn("space-y-3", className)}>
      {backHref ? (
        <BackLink href={backHref} label={backLabel} variant="trail" className="mb-1" />
      ) : null}
      {eyebrow ? (
        <p className="text-xs font-bold uppercase tracking-widest text-accent">{eyebrow}</p>
      ) : null}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-foreground">{title}</h1>
          {description ? (
            <p className="mt-3 max-w-2xl text-base leading-relaxed text-muted">{description}</p>
          ) : null}
        </div>
        {actions ? <div className="flex flex-wrap gap-2">{actions}</div> : null}
      </div>
    </header>
  );
}
