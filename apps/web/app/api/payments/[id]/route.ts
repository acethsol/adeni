import { NextResponse } from "next/server";
import { paymentIntentResponseSchema } from "@adeni/shared";
import { customerApiFetch } from "@/lib/customer-api";

export async function GET(
  _request: Request,
  context: { params: Promise<{ id: string }> },
) {
  const { id } = await context.params;
  const response = await customerApiFetch(`/api/v1/payments/${id}`);

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const parsed = paymentIntentResponseSchema.safeParse(payload);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid payment response." }, { status: 502 });
  }

  return NextResponse.json(parsed.data);
}
