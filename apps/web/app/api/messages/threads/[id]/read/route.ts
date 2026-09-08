import { NextResponse } from "next/server";
import { createCustomerApiClient } from "@/lib/customer-api";

type Params = { params: Promise<{ id: string }> };

export async function POST(_request: Request, { params }: Params) {
  try {
    const { id } = await params;
    const client = await createCustomerApiClient();
    await client.markCustomerThreadRead(id);
    return new NextResponse(null, { status: 204 });
  } catch {
    return NextResponse.json({ title: "Could not mark thread read." }, { status: 502 });
  }
}
