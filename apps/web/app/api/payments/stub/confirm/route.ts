import { NextResponse } from "next/server";
import { paymentIntentResponseSchema } from "@adeni/shared";
import { getApiBaseUrl } from "@/lib/adeni";
import { isProductionDeployment } from "@/lib/env";

export async function POST(request: Request) {
  if (isProductionDeployment()) {
    return NextResponse.json({ title: "Not found." }, { status: 404 });
  }
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const reference = typeof body === "object" && body && "reference" in body
    ? String((body as { reference?: string }).reference ?? "")
    : "";

  if (!reference) {
    return NextResponse.json({ title: "Reference is required." }, { status: 400 });
  }

  const response = await fetch(`${getApiBaseUrl()}/api/v1/payments/stub/confirm`, {
    method: "POST",
    headers: { Accept: "application/json", "Content-Type": "application/json" },
    body: JSON.stringify({ reference }),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const parsed = paymentIntentResponseSchema.safeParse(payload);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid payment response." }, { status: 502 });
  }

  return NextResponse.json(parsed.data);
}
