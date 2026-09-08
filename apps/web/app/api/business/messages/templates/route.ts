import { NextResponse } from "next/server";
import { messageTemplatesResponseSchema } from "@adeni/shared";
import { createBusinessApiClient } from "@/lib/business-api";

export async function GET() {
  try {
    const client = await createBusinessApiClient();
    const items = await client.getTenantMessageTemplates();
    const parsed = messageTemplatesResponseSchema.safeParse({ items });
    if (!parsed.success) {
      return NextResponse.json({ title: "Invalid templates response." }, { status: 502 });
    }

    return NextResponse.json(parsed.data);
  } catch {
    return NextResponse.json({ items: [] });
  }
}
