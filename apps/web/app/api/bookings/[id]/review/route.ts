import { NextResponse } from "next/server";
import { createReviewRequestSchema, reviewResponseSchema } from "@adeni/shared";
import { customerApiFetch } from "@/lib/customer-api";

type Params = { params: Promise<{ id: string }> };

export async function POST(request: Request, { params }: Params) {
  const { id } = await params;
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = createReviewRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid review request." }, { status: 400 });
  }

  const response = await customerApiFetch(`/api/v1/bookings/${encodeURIComponent(id)}/review`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const review = reviewResponseSchema.safeParse(payload);
  if (!review.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(review.data, { status: 201 });
}
