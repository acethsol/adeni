import { NextResponse } from "next/server";
import { unreadCountResponseSchema } from "@adeni/shared";
import { createBusinessApiClient } from "@/lib/business-api";

export async function GET() {
  try {
    const client = await createBusinessApiClient();
    const count = await client.getTenantMessageUnreadCount();
    const parsed = unreadCountResponseSchema.safeParse({ count });
    if (!parsed.success) {
      return NextResponse.json({ title: "Invalid unread count." }, { status: 502 });
    }

    return NextResponse.json(parsed.data);
  } catch {
    return NextResponse.json({ count: 0 });
  }
}
