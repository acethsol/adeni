import { notFound } from "next/navigation";
import { isProductionDeployment } from "@/lib/env";
import { StubCheckoutClient } from "./stub-checkout-client";

export default function StubCheckoutPage() {
  if (isProductionDeployment()) {
    notFound();
  }

  return <StubCheckoutClient />;
}
