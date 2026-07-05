import { NextResponse } from "next/server";
import { createAuthenticatedApiClient } from "@/lib/adeni";

type Params = { params: Promise<{ id: string }> };

export async function POST(_request: Request, { params }: Params) {
  const { id } = await params;

  try {
    const client = await createAuthenticatedApiClient();
    await client.initiateAdminCustomerDelete(id);
    return new NextResponse(null, { status: 204 });
  } catch {
    return NextResponse.json({ title: "Could not initiate customer erasure." }, { status: 502 });
  }
}
