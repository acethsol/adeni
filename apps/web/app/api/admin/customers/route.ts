import { NextResponse } from "next/server";
import { adminCustomersResponseSchema } from "@adeni/shared";
import { createAuthenticatedApiClient } from "@/lib/adeni";

export async function GET(request: Request) {
  const email = new URL(request.url).searchParams.get("email");
  if (!email?.trim()) {
    return NextResponse.json({ title: "Email query parameter is required." }, { status: 400 });
  }

  try {
    const client = await createAuthenticatedApiClient();
    const items = await client.searchAdminCustomers(email.trim());
    const payload = adminCustomersResponseSchema.parse({ items });
    return NextResponse.json(payload);
  } catch {
    return NextResponse.json({ title: "Could not search customers." }, { status: 502 });
  }
}
