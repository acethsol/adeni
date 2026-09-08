import { NextResponse } from "next/server";
import { quoteRequestsResponseSchema } from "@adeni/shared";
import { customerApiFetch } from "@/lib/customer-api";

export async function GET() {
  const response = await customerApiFetch("/api/v1/quotes");

  if (response.status === 401) {
    return NextResponse.json({ title: "Unauthorized" }, { status: 401 });
  }

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const parsed = quoteRequestsResponseSchema.safeParse(payload);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(parsed.data);
}
