import { NextResponse } from "next/server";
import { publicReviewItemSchema, replyToReviewRequestSchema } from "@adeni/shared";
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

  const parsed = replyToReviewRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid reply." }, { status: 400 });
  }

  const response = await businessApiFetch(`/api/v1/tenant/reviews/${encodeURIComponent(id)}/reply`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const review = publicReviewItemSchema.safeParse(payload);
  if (!review.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(review.data);
}
