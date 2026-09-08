import { NextResponse } from "next/server";
import { messageThreadsResponseSchema } from "@adeni/shared";
import { createBusinessApiClient } from "@/lib/business-api";

export async function GET() {
  try {
    const client = await createBusinessApiClient();
    const items = await client.listTenantMessageThreads();
    const parsed = messageThreadsResponseSchema.safeParse({ items });
    if (!parsed.success) {
      return NextResponse.json({ title: "Invalid threads response." }, { status: 502 });
    }

    return NextResponse.json(parsed.data);
  } catch {
    return NextResponse.json({ title: "Could not load messages." }, { status: 502 });
  }
}
