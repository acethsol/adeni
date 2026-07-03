import { NextResponse } from "next/server";
import { submitVerificationRequestSchema } from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

export async function POST(request: Request) {
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = submitVerificationRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid verification request." }, { status: 400 });
  }

  const response = await businessApiFetch("/api/v1/tenant/verification", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => ({}));
    return NextResponse.json(payload, { status: response.status });
  }

  return new NextResponse(null, { status: 204 });
}
