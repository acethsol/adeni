import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/cn";

type Props = HTMLAttributes<HTMLDivElement> & {
  children: ReactNode;
  padding?: "none" | "sm" | "md" | "lg";
  interactive?: boolean;
};

const paddingClasses = {
  none: "",
  sm: "p-4",
  md: "p-5",
  lg: "p-6",
};

export function Card({
  children,
  className,
  padding = "md",
  interactive = false,
  ...rest
}: Props) {
  return (
    <div
      className={cn(
        "rounded-xl border border-border bg-surface shadow-sm",
        paddingClasses[padding],
        interactive && "transition-all hover:border-border-strong hover:shadow-md",
        className,
      )}
      {...rest}
    >
      {children}
    </div>
  );
}

export function CardTitle({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return <h2 className={cn("text-lg font-semibold text-foreground", className)}>{children}</h2>;
}

export function CardDescription({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <p className={cn("mt-2 text-sm leading-relaxed text-muted", className)}>{children}</p>
  );
}
