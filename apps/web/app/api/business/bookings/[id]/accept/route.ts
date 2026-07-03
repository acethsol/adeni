import { NextResponse } from "next/server";
import { bookingResponseSchema } from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

type Params = { params: Promise<{ id: string }> };

export async function POST(_request: Request, { params }: Params) {
  const { id } = await params;
  const response = await businessApiFetch(
    `/api/v1/tenant/bookings/${encodeURIComponent(id)}/accept`,
    { method: "POST" },
  );

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const parsed = bookingResponseSchema.safeParse(payload);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(parsed.data);
}
