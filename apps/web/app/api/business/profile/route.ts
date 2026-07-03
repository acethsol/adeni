import { NextResponse } from "next/server";
import { businessProfileSchema, updateBusinessProfileRequestSchema } from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

export async function PATCH(request: Request) {
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = updateBusinessProfileRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid profile request." }, { status: 400 });
  }

  const response = await businessApiFetch("/api/v1/tenant/profile", {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const profile = businessProfileSchema.safeParse(payload);
  if (!profile.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(profile.data);
}
