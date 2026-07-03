import { NextResponse } from "next/server";
import { weeklyAvailabilityResponseSchema } from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

export async function GET() {
  const response = await businessApiFetch("/api/v1/tenant/availability");

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const parsed = weeklyAvailabilityResponseSchema.safeParse(payload);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(parsed.data);
}

export async function PUT(request: Request) {
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const response = await businessApiFetch("/api/v1/tenant/availability", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const parsed = weeklyAvailabilityResponseSchema.safeParse(payload);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(parsed.data);
}
