import { NextResponse } from "next/server";
import { initializePaymentRequestSchema, paymentIntentResponseSchema } from "@adeni/shared";
import { getApiBaseUrl } from "@/lib/adeni";
import { getAccessToken } from "@/lib/auth/session";
import { isAuth0Configured } from "@/lib/auth/config";

async function customerHeaders(): Promise<Headers | NextResponse> {
  const headers = new Headers({
    Accept: "application/json",
    "Content-Type": "application/json",
  });
  const accessToken = await getAccessToken();

  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
    return headers;
  }

  if (!isAuth0Configured() && process.env.DEV_CUSTOMER_AUTH0_SUB) {
    headers.set("X-Dev-Auth0-Sub", process.env.DEV_CUSTOMER_AUTH0_SUB);
    return headers;
  }

  return NextResponse.json({ title: "Sign in to continue." }, { status: 401 });
}

export async function POST(request: Request) {
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = initializePaymentRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid payment request." }, { status: 400 });
  }

  const headersOrResponse = await customerHeaders();
  if (headersOrResponse instanceof NextResponse) {
    return headersOrResponse;
  }

  const response = await fetch(`${getApiBaseUrl()}/api/v1/payments/initialize`, {
    method: "POST",
    headers: headersOrResponse,
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
