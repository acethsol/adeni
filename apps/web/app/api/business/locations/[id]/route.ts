import { NextResponse } from "next/server";
import {
  businessLocationSchema,
  upsertBusinessLocationRequestSchema,
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

  const parsed = upsertBusinessLocationRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid location request." }, { status: 400 });
  }

  const response = await businessApiFetch(
    `/api/v1/tenant/locations/${encodeURIComponent(id)}`,
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

  const location = businessLocationSchema.safeParse(payload);
  if (!location.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(location.data);
}

export async function DELETE(_request: Request, { params }: Params) {
  const { id } = await params;

  const response = await businessApiFetch(
    `/api/v1/tenant/locations/${encodeURIComponent(id)}`,
    { method: "DELETE" },
  );

  if (!response.ok) {
    const payload = await response.json().catch(() => ({}));
    return NextResponse.json(payload, { status: response.status });
  }

  return new NextResponse(null, { status: 204 });
}
