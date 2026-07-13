import Link from "next/link";
import { ChevronLeft } from "lucide-react";
import { cn } from "@/lib/cn";

type BaseProps = {
  label: string;
  hint?: string;
  className?: string;
  variant?: "chip" | "trail";
};

type LinkProps = BaseProps & {
  href: string;
  onClick?: never;
  disabled?: never;
};

type ButtonProps = BaseProps & {
  href?: never;
  onClick: () => void;
  disabled?: boolean;
};

type Props = LinkProps | ButtonProps;

const shellClasses: Record<NonNullable<Props["variant"]>, string> = {
  chip:
    "rounded-full border border-border bg-subtle/90 px-1.5 py-1.5 pr-5 shadow-sm transition-all hover:border-accent/30 hover:bg-surface hover:shadow-md",
  trail:
    "rounded-full px-1 py-1 transition-colors hover:bg-subtle/80",
};

const iconShellClasses: Record<NonNullable<Props["variant"]>, string> = {
  chip:
    "bg-surface shadow-sm ring-1 ring-border transition-transform group-hover:-translate-x-0.5 group-hover:ring-accent/25",
  trail:
    "bg-accent/10 ring-1 ring-accent/15 transition-transform group-hover:-translate-x-0.5 group-hover:bg-accent/15",
};

function BackLinkContent({
  label,
  hint,
  variant = "chip",
}: Pick<Props, "label" | "hint" | "variant">) {
  return (
    <>
      <span
        className={cn(
          "flex h-8 w-8 shrink-0 items-center justify-center rounded-full",
          iconShellClasses[variant],
        )}
        aria-hidden
      >
        <ChevronLeft className="h-4 w-4 text-accent" strokeWidth={2.5} />
      </span>
      <span className="min-w-0 text-left">
        <span className="block text-sm font-semibold leading-tight text-foreground">{label}</span>
        {hint ? <span className="mt-0.5 block text-xs leading-tight text-muted">{hint}</span> : null}
      </span>
    </>
  );
}

export function BackLink({
  label,
  hint,
  className,
  variant = "chip",
  ...rest
}: Props) {
  const classes = cn(
    "group inline-flex max-w-full items-center gap-3 text-left",
    shellClasses[variant],
    className,
  );

  if ("href" in rest && rest.href) {
    return (
      <Link href={rest.href} className={classes}>
        <BackLinkContent label={label} hint={hint} variant={variant} />
      </Link>
    );
  }

  return (
    <button
      type="button"
      onClick={rest.onClick}
      disabled={rest.disabled}
      className={cn(classes, rest.disabled && "cursor-not-allowed opacity-60")}
    >
      <BackLinkContent label={label} hint={hint} variant={variant} />
    </button>
  );
}
