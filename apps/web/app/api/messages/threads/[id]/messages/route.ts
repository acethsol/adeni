import { NextResponse } from "next/server";
import { sendMessageRequestSchema } from "@adeni/shared";
import { createCustomerApiClient } from "@/lib/customer-api";

type Params = { params: Promise<{ id: string }> };

export async function POST(request: Request, { params }: Params) {
  try {
    const { id } = await params;
    const body = sendMessageRequestSchema.parse(await request.json());
    const client = await createCustomerApiClient();
    const message = await client.sendCustomerMessage(id, body);
    return NextResponse.json(message, { status: 201 });
  } catch (error) {
    const message = error instanceof Error ? error.message : "Could not send message.";
    return NextResponse.json({ title: message }, { status: 400 });
  }
}
