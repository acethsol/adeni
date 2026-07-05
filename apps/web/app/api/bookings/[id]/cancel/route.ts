import { NextResponse } from "next/server";
import { customerBookingResponseSchema } from "@adeni/shared";
import { customerApiFetch } from "@/lib/customer-api";

type Params = { params: Promise<{ id: string }> };

export async function POST(_request: Request, { params }: Params) {
  const { id } = await params;
  const response = await customerApiFetch(
    `/api/v1/bookings/${encodeURIComponent(id)}/cancel`,
    { method: "POST" },
  );

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const parsed = customerBookingResponseSchema.safeParse(payload);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid booking response." }, { status: 502 });
  }

  return NextResponse.json(parsed.data);
}
