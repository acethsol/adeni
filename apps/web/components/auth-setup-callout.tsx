export function AuthSetupCallout() {
  return (
    <div className="rounded-xl border border-amber-200 bg-amber-50 p-5 text-sm text-amber-950">
      <p className="font-semibold">Auth0 is not configured for this environment.</p>
      <p className="mt-2 leading-relaxed">
        Copy <code className="rounded bg-white px-1">apps/web/.env.local.example</code> to{" "}
        <code className="rounded bg-white px-1">.env.local</code>, fill in your Auth0 Regular
        Web Application credentials, then restart the dev server. See{" "}
        <code className="rounded bg-white px-1">docs/auth0-setup.md</code> in the repo.
      </p>
    </div>
  );
}
