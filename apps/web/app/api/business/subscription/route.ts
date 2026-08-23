import { NextResponse } from "next/server";
import { createBusinessApiClient } from "@/lib/business-api";

export async function GET() {
  try {
    const client = await createBusinessApiClient();
    const usage = await client.getTenantSubscriptionUsage();
    return NextResponse.json(usage);
  } catch {
    return NextResponse.json({ title: "Unauthorized" }, { status: 401 });
  }
}
