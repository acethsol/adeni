import { cn } from "@/lib/cn";

type Props = {
  size?: "sm" | "md" | "lg";
  className?: string;
  label?: string;
};

const sizeClasses = {
  sm: "h-4 w-4 border-2",
  md: "h-6 w-6 border-2",
  lg: "h-9 w-9 border-[3px]",
};

export function LoadingSpinner({ size = "md", className, label = "Loading" }: Props) {
  return (
    <span
      role="status"
      aria-label={label}
      className={cn(
        "inline-block shrink-0 animate-spin rounded-full border-accent/25 border-t-accent",
        sizeClasses[size],
        className,
      )}
    />
  );
}
