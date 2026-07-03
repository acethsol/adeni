import { NextResponse } from "next/server";
import {
  createServiceOfferingRequestSchema,
  serviceOfferingSchema,
} from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

export async function POST(request: Request) {
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = createServiceOfferingRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid service request." }, { status: 400 });
  }

  const response = await businessApiFetch("/api/v1/tenant/services", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const service = serviceOfferingSchema.safeParse(payload);
  if (!service.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(service.data, { status: 201 });
}
