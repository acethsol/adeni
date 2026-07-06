import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

type Tone = "default" | "accent" | "success" | "warning" | "destructive";

const toneClasses: Record<Tone, string> = {
  default: "border-border bg-surface text-foreground",
  accent: "border-accent/20 bg-accent/10 text-accent",
  success: "border-accent/20 bg-accent/10 text-accent",
  warning: "border-amber-500/20 bg-amber-50 text-amber-900",
  destructive: "border-destructive/20 bg-destructive-bg text-destructive",
};

export function Badge({
  children,
  tone = "default",
  className,
}: {
  children: ReactNode;
  tone?: Tone;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold",
        toneClasses[tone],
        className,
      )}
    >
      {children}
    </span>
  );
}
