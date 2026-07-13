import type { ReactNode } from "react";
import { Briefcase } from "lucide-react";
import { PublicHeader } from "@/components/public-header";
import { BusinessPortalNav } from "@/components/business-portal-nav";
import { Callout } from "@/components/ui/callout";
import { publicContainerClass, publicHeroBandClass } from "@/lib/layout-classes";
import { cn } from "@/lib/cn";

type Props = {
  title: string;
  description: string;
  devMode?: boolean;
  hasBusiness?: boolean;
  actions?: ReactNode;
  children: ReactNode;
};

export function BusinessPortalShell({
  title,
  description,
  devMode = false,
  hasBusiness = false,
  actions,
  children,
}: Props) {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <PublicHeader searchMode="compact" />

      <main id="main-content">
        <section className={cn(publicHeroBandClass, "border-b")}>
          <div className={cn(publicContainerClass, "py-10 lg:py-12")}>
            <div className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
              <div className="flex gap-4">
                <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-md">
                  <Briefcase className="h-7 w-7" aria-hidden />
                </div>
                <div>
                  <p className="text-xs font-bold uppercase tracking-widest text-accent">
                    Business portal
                  </p>
                  <h1 className="mt-2 text-3xl font-bold tracking-tight text-foreground sm:text-4xl">
                    {title}
                  </h1>
                  <p className="mt-3 max-w-2xl text-base leading-relaxed text-muted">
                    {description}
                  </p>
                </div>
              </div>
              {actions ? <div className="flex flex-wrap gap-2">{actions}</div> : null}
            </div>

            {devMode ? (
              <Callout tone="success" className="mt-6">
                Local dev mode — using <code className="text-xs">DEV_BUSINESS_AUTH0_SUB</code>{" "}
                (linked to <code className="text-xs">lekki-cuts</code> in dev seed).
              </Callout>
            ) : null}
          </div>
        </section>

        <div className={cn(publicContainerClass, "py-10 lg:py-12")}>
          <div className="grid gap-8 lg:grid-cols-[220px_minmax(0,1fr)] lg:gap-12">
            <aside className="lg:sticky lg:top-24 lg:self-start">
              <BusinessPortalNav showRegister={!hasBusiness} />
            </aside>
            <div>{children}</div>
          </div>
        </div>
      </main>
    </div>
  );
}
