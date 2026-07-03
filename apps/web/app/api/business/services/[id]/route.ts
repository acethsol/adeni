import { NextResponse } from "next/server";
import {
  serviceOfferingSchema,
  updateServiceOfferingRequestSchema,
} from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

type Params = { params: Promise<{ id: string }> };

export async function PATCH(request: Request, { params }: Params) {
  const { id } = await params;
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = updateServiceOfferingRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid service request." }, { status: 400 });
  }

  const response = await businessApiFetch(
    `/api/v1/tenant/services/${encodeURIComponent(id)}`,
    {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(parsed.data),
    },
  );

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const service = serviceOfferingSchema.safeParse(payload);
  if (!service.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(service.data);
}

export async function DELETE(_request: Request, { params }: Params) {
  const { id } = await params;

  const response = await businessApiFetch(
    `/api/v1/tenant/services/${encodeURIComponent(id)}`,
    { method: "DELETE" },
  );

  if (!response.ok) {
    const payload = await response.json().catch(() => ({}));
    return NextResponse.json(payload, { status: response.status });
  }

  return new NextResponse(null, { status: 204 });
}
