import Link from "next/link";
import type { ReactNode } from "react";
import { PublicHeader } from "@/components/public-header";
import { Callout } from "@/components/ui/callout";
import { publicContainerClass } from "@/lib/layout-classes";

type Props = {
  title: string;
  description: string;
  children: ReactNode;
};

export function LegalDocumentPage({ title, description, children }: Props) {
  return (
    <>
      <PublicHeader searchMode="none" />
      <main id="main-content" className={`${publicContainerClass} py-10 md:py-14`}>
        <div className="mx-auto max-w-3xl">
          <p className="text-sm font-semibold uppercase tracking-widest text-accent">Legal</p>
          <h1 className="mt-2 text-3xl font-bold tracking-tight text-foreground md:text-4xl">
            {title}
          </h1>
          <p className="mt-3 text-muted">{description}</p>

          <Callout tone="warning" title="Draft placeholder" className="mt-8">
            This page is a structural placeholder pending review by qualified legal counsel.
            Do not treat it as binding policy. For privacy requests before launch, contact your
            Adeni administrator.
          </Callout>

          <article className="prose prose-neutral mt-8 max-w-none text-foreground prose-headings:text-foreground prose-p:text-muted prose-li:text-muted">
            {children}
          </article>

          <p className="mt-10 text-sm text-muted">
            <Link href="/" className="font-medium text-accent hover:underline">
              ← Back to home
            </Link>
          </p>
        </div>
      </main>
    </>
  );
}
