import { NextResponse } from "next/server";
import { createBusinessApiClient } from "@/lib/business-api";

type Params = { params: Promise<{ id: string }> };

export async function POST(_request: Request, { params }: Params) {
  const { id } = await params;

  try {
    const client = await createBusinessApiClient();
    await client.markTenantThreadRead(id);
    return new NextResponse(null, { status: 204 });
  } catch {
    return NextResponse.json({ title: "Could not mark read." }, { status: 502 });
  }
}
