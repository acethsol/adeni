import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

type Tone = "info" | "success" | "warning" | "error";

const toneClasses: Record<Tone, string> = {
  info: "border-border bg-surface text-muted",
  success: "border-accent/20 bg-accent/5 text-foreground",
  warning: "border-amber-500/20 bg-amber-50 text-amber-950",
  error: "border-destructive/20 bg-destructive-bg text-destructive",
};

export function Callout({
  title,
  children,
  tone = "info",
  className,
}: {
  title?: string;
  children: ReactNode;
  tone?: Tone;
  className?: string;
}) {
  return (
    <div className={cn("rounded-xl border px-4 py-3 text-sm leading-relaxed", toneClasses[tone], className)}>
      {title ? <p className="font-semibold text-foreground">{title}</p> : null}
      <div className={title ? "mt-1" : undefined}>{children}</div>
    </div>
  );
}
