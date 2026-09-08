import { NextResponse } from "next/server";
import { messageThreadsResponseSchema } from "@adeni/shared";
import { createCustomerApiClient } from "@/lib/customer-api";

export async function GET() {
  try {
    const client = await createCustomerApiClient();
    const items = await client.listCustomerMessageThreads();
    const parsed = messageThreadsResponseSchema.safeParse({ items });
    if (!parsed.success) {
      return NextResponse.json({ title: "Invalid threads response." }, { status: 502 });
    }

    return NextResponse.json(parsed.data);
  } catch {
    return NextResponse.json({ title: "Could not load messages." }, { status: 502 });
  }
}

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const client = await createCustomerApiClient();
    const thread = await client.createMessageThread(body);
    return NextResponse.json(thread, { status: 201 });
  } catch (error) {
    const message = error instanceof Error ? error.message : "Could not start conversation.";
    return NextResponse.json({ title: message }, { status: 400 });
  }
}
