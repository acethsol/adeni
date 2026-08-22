import { NextResponse } from "next/server";
import { joinWaitlistRequestSchema } from "@adeni/shared";
import { createApiClient } from "@/lib/adeni";

export async function POST(request: Request) {
  try {
    const body = joinWaitlistRequestSchema.parse(await request.json());
    const client = createApiClient();
    const entry = await client.joinWaitlist(body);
    return NextResponse.json(entry, { status: 201 });
  } catch (error) {
    const message = error instanceof Error ? error.message : "Could not join waitlist.";
    return NextResponse.json({ title: message }, { status: 400 });
  }
}
