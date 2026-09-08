import { NextResponse } from "next/server";
import { messageThreadDetailSchema } from "@adeni/shared";
import { createBusinessApiClient } from "@/lib/business-api";

type Params = { params: Promise<{ id: string }> };

export async function GET(_request: Request, { params }: Params) {
  const { id } = await params;

  try {
    const client = await createBusinessApiClient();
    const detail = await client.getTenantMessageThread(id);
    const parsed = messageThreadDetailSchema.safeParse(detail);
    if (!parsed.success) {
      return NextResponse.json({ title: "Invalid thread response." }, { status: 502 });
    }

    return NextResponse.json(parsed.data);
  } catch {
    return NextResponse.json({ title: "Could not load conversation." }, { status: 502 });
  }
}
