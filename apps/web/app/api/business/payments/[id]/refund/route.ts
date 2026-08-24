import { NextResponse } from "next/server";
import { paymentIntentResponseSchema, refundPaymentRequestSchema } from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

export async function POST(
  request: Request,
  context: { params: Promise<{ id: string }> },
) {
  const { id } = await context.params;
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = refundPaymentRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid refund request." }, { status: 400 });
  }

  const response = await businessApiFetch(`/api/v1/payments/${id}/refund`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const payment = paymentIntentResponseSchema.safeParse(payload);
  if (!payment.success) {
    return NextResponse.json({ title: "Invalid payment response." }, { status: 502 });
  }

  return NextResponse.json(payment.data);
}
