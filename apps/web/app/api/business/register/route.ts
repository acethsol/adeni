import { NextResponse } from "next/server";
import { registerBusinessRequestSchema, registerBusinessResponseSchema } from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

export async function POST(request: Request) {
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = registerBusinessRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid registration request." }, { status: 400 });
  }

  const response = await businessApiFetch("/api/v1/tenant/register", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const registered = registerBusinessResponseSchema.safeParse(payload);
  if (!registered.success) {
    return NextResponse.json({ title: "Invalid API response." }, { status: 502 });
  }

  return NextResponse.json(registered.data);
}
