import { NextResponse } from "next/server";
import { quoteRequestResponseSchema } from "@adeni/shared";
import { customerApiFetch } from "@/lib/customer-api";

type Params = { params: Promise<{ id: string }> };

export async function POST(_request: Request, { params }: Params) {
  const { id } = await params;

  const response = await customerApiFetch(`/api/v1/quotes/${encodeURIComponent(id)}/accept`, {
    method: "POST",
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const parsed = quoteRequestResponseSchema.safeParse(payload);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(parsed.data);
}
