import { NextResponse } from "next/server";
import { createAuthenticatedApiClient } from "@/lib/adeni";

export async function GET() {
  try {
    const client = await createAuthenticatedApiClient();
    const items = await client.getPendingBusinesses();
    return NextResponse.json({ items });
  } catch {
    return NextResponse.json({ title: "Unauthorized" }, { status: 401 });
  }
}
