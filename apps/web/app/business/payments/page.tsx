import { BusinessPaymentsPanel } from "@/components/business-payments-panel";
import { createBusinessApiClient } from "@/lib/business-api";
import { redirect } from "next/navigation";

export default async function BusinessPaymentsPage() {
  const client = await createBusinessApiClient();
  let profile;
  try {
    profile = await client.getTenantProfile();
  } catch {
    redirect("/business/register");
  }

  return (
    <div className="mx-auto w-full max-w-[1600px] px-4 py-8 sm:px-6 lg:px-10 lg:py-10">
      <h1 className="text-2xl font-bold tracking-tight">Payments</h1>
      <p className="mt-1 text-sm text-muted">
        Create payment links, track transactions, and manage refunds.
      </p>
      <div className="mt-8">
        <BusinessPaymentsPanel profile={profile} />
      </div>
    </div>
  );
}
