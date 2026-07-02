import { NextResponse } from "next/server";
import { createBookingRequestSchema } from "@adeni/shared";
import { getApiBaseUrl } from "@/lib/adeni";
import { getAccessToken } from "@/lib/auth/session";
import { isAuth0Configured } from "@/lib/auth/config";

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
