import { NextResponse } from "next/server";
import {
  businessLocationSchema,
  tenantLocationsResponseSchema,
  upsertBusinessLocationRequestSchema,
} from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

export async function GET() {
  const response = await businessApiFetch("/api/v1/tenant/locations");

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const parsed = tenantLocationsResponseSchema.safeParse(payload);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(parsed.data);
}

export async function POST(request: Request) {
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = upsertBusinessLocationRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid location request." }, { status: 400 });
  }

  const response = await businessApiFetch("/api/v1/tenant/locations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const location = businessLocationSchema.safeParse(payload);
  if (!location.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(location.data, { status: 201 });
}
