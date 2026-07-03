import { NextResponse } from "next/server";
import { createBookingRequestSchema, customerBookingsResponseSchema } from "@adeni/shared";
import { getApiBaseUrl } from "@/lib/adeni";
import { getAccessToken } from "@/lib/auth/session";
import { isAuth0Configured } from "@/lib/auth/config";

async function resolveCustomerHeaders(): Promise<Headers | NextResponse> {
  const headers = new Headers({ Accept: "application/json" });
  const accessToken = await getAccessToken();

  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
    return headers;
  }

  if (!isAuth0Configured() && process.env.DEV_CUSTOMER_AUTH0_SUB) {
    headers.set("X-Dev-Auth0-Sub", process.env.DEV_CUSTOMER_AUTH0_SUB);
    return headers;
  }

  return NextResponse.json(
    { title: "Sign in to view your bookings." },
    { status: 401 },
  );
}

export async function GET() {
  const headersOrResponse = await resolveCustomerHeaders();
  if (headersOrResponse instanceof NextResponse) {
    return headersOrResponse;
  }

  const response = await fetch(`${getApiBaseUrl()}/api/v1/bookings`, {
    headers: headersOrResponse,
  });

  const payload = await response.json().catch(() => ({}));

  if (!response.ok) {
    return NextResponse.json(payload, { status: response.status });
  }

  const parsed = customerBookingsResponseSchema.safeParse(payload);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid booking list response." }, { status: 502 });
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

  const parsed = createBookingRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Invalid booking request." }, { status: 400 });
  }

  const headers = new Headers({
    Accept: "application/json",
    "Content-Type": "application/json",
  });

  const accessToken = await getAccessToken();
  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  } else if (!isAuth0Configured() && process.env.DEV_CUSTOMER_AUTH0_SUB) {
    headers.set("X-Dev-Auth0-Sub", process.env.DEV_CUSTOMER_AUTH0_SUB);
  } else {
    return NextResponse.json(
      { title: "Sign in to book an appointment." },
      { status: 401 },
    );
  }

  const response = await fetch(`${getApiBaseUrl()}/api/v1/bookings`, {
    method: "POST",
    headers,
    body: JSON.stringify(parsed.data),
  });

  const payload = await response.json().catch(() => ({}));
  return NextResponse.json(payload, { status: response.status });
}
