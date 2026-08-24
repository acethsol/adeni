import { NextResponse } from "next/server";
import {
  createPaymentLinkRequestSchema,
  paymentIntentResponseSchema,
  paymentLedgerResponseSchema,
  refundPaymentRequestSchema,
} from "@adeni/shared";
import { businessApiFetch } from "@/lib/business-api";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const query = url.searchParams.toString();
  const path = query ? `/api/v1/payments/ledger?${query}` : "/api/v1/payments/ledger";
  const response = await businessApiFetch(path);

  if (response.status === 401) {
    return NextResponse.json({ title: "Unauthorized" }, { status: 401 });
  }

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const parsed = paymentLedgerResponseSchema.safeParse(payload);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid ledger response." }, { status: 502 });
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

  const parsed = createPaymentLinkRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid payment link request." }, { status: 400 });
  }

  const response = await businessApiFetch("/api/v1/payments/links", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const payment = paymentIntentResponseSchema.safeParse(payload);
  if (!payment.success) {
    return NextResponse.json({ title: "Invalid payment response." }, { status: 502 });
  }

  return NextResponse.json(payment.data);
}
