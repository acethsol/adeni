import { NextResponse } from "next/server";
import { messageThreadDetailSchema } from "@adeni/shared";
import { createCustomerApiClient } from "@/lib/customer-api";

type Params = { params: Promise<{ id: string }> };

export async function GET(_request: Request, { params }: Params) {
  try {
    const { id } = await params;
    const client = await createCustomerApiClient();
    const detail = await client.getCustomerMessageThread(id);
    const parsed = messageThreadDetailSchema.safeParse(detail);
    if (!parsed.success) {
      return NextResponse.json({ title: "Invalid thread response." }, { status: 502 });
    }

    return NextResponse.json(parsed.data);
  } catch {
    return NextResponse.json({ title: "Could not load conversation." }, { status: 502 });
  }
}
