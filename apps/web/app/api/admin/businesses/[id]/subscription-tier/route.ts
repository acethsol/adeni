import { NextResponse } from "next/server";
import { createAuthenticatedApiClient } from "@/lib/adeni";

type Params = { params: Promise<{ id: string }> };

export async function PATCH(request: Request, { params }: Params) {
  try {
    const { id } = await params;
    const body = (await request.json()) as { tier: "free" | "pro" | "business" };
    const client = await createAuthenticatedApiClient();
    await client.setAdminBusinessSubscriptionTier(id, body.tier);
    return new NextResponse(null, { status: 204 });
  } catch {
    return NextResponse.json({ title: "Unauthorized" }, { status: 401 });
  }
}
