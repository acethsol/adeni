"use client";

import type { ReactNode } from "react";
import { useBusinessSidebar } from "@/contexts/business-sidebar-context";
import { cn } from "@/lib/cn";

type Props = {
  topbar?: ReactNode;
  children: ReactNode;
};

export function BusinessPortalContent({ topbar, children }: Props) {
  const { collapsed } = useBusinessSidebar();

  return (
    <div className={cn("transition-[padding] duration-200", collapsed ? "lg:pl-20" : "lg:pl-64")}>
      {topbar}
      <main id="main-content">{children}</main>
    </div>
  );
}
