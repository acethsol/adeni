import { NextResponse } from "next/server";
import { quoteRequestResponseSchema, submitQuoteOfferRequestSchema } from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

type Params = { params: Promise<{ id: string }> };

export async function POST(request: Request, { params }: Params) {
  const { id } = await params;

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = submitQuoteOfferRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid quote offer." }, { status: 400 });
  }

  const response = await businessApiFetch(`/api/v1/tenant/quotes/${encodeURIComponent(id)}/offer`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const quote = quoteRequestResponseSchema.safeParse(payload);
  if (!quote.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(quote.data);
}
