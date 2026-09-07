import { NextResponse } from "next/server";
import {
  requestVerificationBadgeRequestSchema,
  verificationBadgeSchema,
} from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

export async function GET() {
  const response = await businessApiFetch("/api/v1/tenant/verification/badges");

  if (response.status === 401) {
    return NextResponse.json({ title: "Unauthorized" }, { status: 401 });
  }

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  return NextResponse.json(payload);
}

export async function POST(request: Request) {
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = requestVerificationBadgeRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid badge request." }, { status: 400 });
  }

  const response = await businessApiFetch("/api/v1/tenant/verification/badges", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const badge = verificationBadgeSchema.safeParse(payload);
  if (!badge.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(badge.data, { status: 201 });
}
