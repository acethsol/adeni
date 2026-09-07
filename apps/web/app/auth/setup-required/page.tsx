import Link from "next/link";

type Props = {
  searchParams: Promise<{ returnTo?: string }>;
};

export default async function AuthSetupRequiredPage({ searchParams }: Props) {
  const params = await searchParams;
  const returnTo = params.returnTo?.startsWith("/") ? params.returnTo : "/business";

  return (
    <main className="mx-auto flex min-h-[60vh] max-w-lg flex-col justify-center px-6 py-16">
      <p className="text-sm font-semibold uppercase tracking-widest text-accent">Configuration required</p>
      <h1 className="mt-2 text-2xl font-bold text-foreground">Authentication is not configured</h1>
      <p className="mt-4 text-sm text-muted">
        This deployment requires Auth0. Set <code className="text-xs">AUTH0_DOMAIN</code>,{" "}
        <code className="text-xs">AUTH0_CLIENT_ID</code>, <code className="text-xs">AUTH0_CLIENT_SECRET</code>,{" "}
        <code className="text-xs">AUTH0_SECRET</code>, and <code className="text-xs">APP_BASE_URL</code> before
        opening protected areas.
      </p>
      <p className="mt-4 text-sm text-muted">
        Dev impersonation headers are disabled in production builds.
      </p>
      <div className="mt-8 flex flex-wrap gap-3">
        <Link
          href="/"
          className="inline-flex rounded-full bg-primary px-6 py-3 text-sm font-semibold text-primary-foreground"
        >
          Back to home
        </Link>
        <Link
          href={returnTo}
          className="inline-flex rounded-full border border-border px-6 py-3 text-sm font-semibold text-foreground"
        >
          Retry
        </Link>
      </div>
    </main>
  );
}
