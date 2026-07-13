import { NextResponse } from "next/server";

const apiBaseUrl = process.env.ADENI_API_URL ?? "http://localhost:5169";

export async function GET(request: Request) {
  const { search } = new URL(request.url);

  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/discovery${search}`, {
      headers: { Accept: "application/json" },
      cache: "no-store",
    });

    const body = await response.text();

    return new NextResponse(body, {
      status: response.status,
      headers: { "Content-Type": "application/json" },
    });
  } catch {
    return NextResponse.json(
      { title: "Could not reach discovery API." },
      { status: 502 },
    );
  }
}
