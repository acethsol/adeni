import { cn } from "@/lib/cn";

type Props = {
  className?: string;
  lines?: number;
};

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn("adeni-skeleton rounded-md", className)} aria-hidden />;
}

export function SkeletonCard({ lines = 3 }: Props) {
  return (
    <div className="space-y-3 rounded-xl border border-border bg-surface p-5 shadow-sm">
      <Skeleton className="h-5 w-1/3" />
      {Array.from({ length: lines }).map((_, index) => (
        <Skeleton key={index} className={cn("h-4", index === lines - 1 ? "w-2/3" : "w-full")} />
      ))}
    </div>
  );
}

export function SkeletonList({ count = 3 }: { count?: number }) {
  return (
    <div className="space-y-3">
      {Array.from({ length: count }).map((_, index) => (
        <SkeletonCard key={index} lines={2} />
      ))}
    </div>
  );
}
