import { NextResponse } from "next/server";
import { updateCoverImageRequestSchema, updateCoverImageResponseSchema } from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

export async function PATCH(request: Request) {
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = updateCoverImageRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid cover image request." }, { status: 400 });
  }

  const response = await businessApiFetch("/api/v1/tenant/profile/cover", {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const result = updateCoverImageResponseSchema.safeParse(payload);
  if (!result.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(result.data);
}
