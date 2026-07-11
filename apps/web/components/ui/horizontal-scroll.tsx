import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

type HorizontalScrollProps = {
  children: ReactNode;
  className?: string;
};

export function HorizontalScroll({ children, className }: HorizontalScrollProps) {
  return (
    <div className={cn("-mx-6 overflow-x-auto px-6 pb-2 [scrollbar-width:thin]", className)}>
      <ul className="flex snap-x snap-mandatory gap-4">{children}</ul>
    </div>
  );
}

export function HorizontalScrollItem({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return <li className={cn("w-[min(85vw,280px)] shrink-0 snap-start", className)}>{children}</li>;
}
