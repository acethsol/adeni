import { NextResponse } from "next/server";
import { createAuthenticatedApiClient } from "@/lib/adeni";

type Params = {
  params: Promise<{ id: string }>;
};

export async function PUT(request: Request, { params }: Params) {
  try {
    const { id } = await params;
    const client = await createAuthenticatedApiClient();
    const body = await request.json();
    const market = await client.updateAdminMarket(id, body);
    return NextResponse.json(market);
  } catch {
    return NextResponse.json({ title: "Unauthorized" }, { status: 401 });
  }
}
