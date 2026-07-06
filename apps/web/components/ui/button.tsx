import Link from "next/link";
import type { ButtonHTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/cn";

type Variant = "primary" | "secondary" | "ghost" | "destructive";
type Size = "sm" | "md" | "lg";

const variantClasses: Record<Variant, string> = {
  primary:
    "bg-primary text-primary-foreground hover:opacity-90 shadow-sm disabled:opacity-60",
  secondary:
    "border border-border-strong bg-surface text-foreground hover:bg-background disabled:opacity-60",
  ghost: "text-accent hover:bg-background disabled:opacity-60",
  destructive:
    "border border-destructive/20 bg-destructive-bg text-destructive hover:opacity-90 disabled:opacity-60",
};

const sizeClasses: Record<Size, string> = {
  sm: "px-3.5 py-2 text-sm",
  md: "px-5 py-2.5 text-sm",
  lg: "px-6 py-3 text-base",
};

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  size?: Size;
  href?: string;
  children: ReactNode;
};

export function Button({
  variant = "primary",
  size = "md",
  href,
  className,
  children,
  ...rest
}: Props) {
  const classes = cn(
    "inline-flex items-center justify-center rounded-full font-semibold transition-opacity",
    variantClasses[variant],
    sizeClasses[size],
    className,
  );

  if (href) {
    return (
      <Link href={href} className={classes}>
        {children}
      </Link>
    );
  }

  return (
    <button type="button" className={classes} {...rest}>
      {children}
    </button>
  );
}
