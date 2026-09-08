import { NextResponse } from "next/server";
import { quoteRequestsResponseSchema } from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

export async function GET() {
  const response = await businessApiFetch("/api/v1/tenant/quotes");

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
