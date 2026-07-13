import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

type Props = {
  children: ReactNode;
  className?: string;
  padding?: "sm" | "md" | "lg";
};

const paddingClass = {
  sm: "p-4",
  md: "p-6",
  lg: "p-8",
};

export function BusinessPortalCard({ children, className, padding = "md" }: Props) {
  return (
    <div
      className={cn(
        "rounded-2xl border border-border bg-surface shadow-sm",
        paddingClass[padding],
        className,
      )}
    >
      {children}
    </div>
  );
}
