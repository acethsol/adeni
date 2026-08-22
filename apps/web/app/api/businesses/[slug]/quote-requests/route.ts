import { NextResponse } from "next/server";
import { createQuoteRequestSchema } from "@adeni/shared";
import { createApiClient } from "@/lib/adeni";

type Params = { params: Promise<{ slug: string }> };

export async function POST(request: Request, { params }: Params) {
  try {
    const { slug } = await params;
    const body = createQuoteRequestSchema.parse(await request.json());
    const client = createApiClient();
    const quote = await client.createQuoteRequest(slug, body);
    return NextResponse.json(quote, { status: 201 });
  } catch (error) {
    const message = error instanceof Error ? error.message : "Could not submit quote request.";
    return NextResponse.json({ title: message }, { status: 400 });
  }
}
