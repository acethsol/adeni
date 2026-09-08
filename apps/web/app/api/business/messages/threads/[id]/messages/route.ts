import { NextResponse } from "next/server";
import { sendMessageRequestSchema, messageResponseSchema } from "@adeni/shared";
import { createBusinessApiClient } from "@/lib/business-api";

type Params = { params: Promise<{ id: string }> };

export async function POST(request: Request, { params }: Params) {
  const { id } = await params;

  try {
    const body = sendMessageRequestSchema.parse(await request.json());
    const client = await createBusinessApiClient();
    const message = await client.sendTenantMessage(id, body);
    const parsed = messageResponseSchema.safeParse(message);
    if (!parsed.success) {
      return NextResponse.json({ title: "Invalid message response." }, { status: 502 });
    }

    return NextResponse.json(parsed.data);
  } catch (error) {
    const message = error instanceof Error ? error.message : "Could not send message.";
    return NextResponse.json({ title: message }, { status: 400 });
  }
}
