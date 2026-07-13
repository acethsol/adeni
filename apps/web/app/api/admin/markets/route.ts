import { NextResponse } from "next/server";
import { createAuthenticatedApiClient } from "@/lib/adeni";

export async function GET() {
  try {
    const client = await createAuthenticatedApiClient();
    const items = await client.getAdminMarkets();
    return NextResponse.json({ items });
  } catch {
    return NextResponse.json({ title: "Unauthorized" }, { status: 401 });
  }
}

export async function POST(request: Request) {
  try {
    const client = await createAuthenticatedApiClient();
    const body = await request.json();
    const market = await client.createAdminMarket(body);
    return NextResponse.json(market, { status: 201 });
  } catch {
    return NextResponse.json({ title: "Unauthorized" }, { status: 401 });
  }
}
