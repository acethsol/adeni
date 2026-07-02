import { NextResponse } from "next/server";
import { formatCoordinatePair } from "@adeni/shared";
import { COORDS_COOKIE_NAME } from "@/lib/market";

type GeoBody = {
  lat?: unknown;
  lng?: unknown;
};

function parseBody(body: GeoBody): { lat: number; lng: number } | null {
  const lat = typeof body.lat === "number" ? body.lat : Number(body.lat);
  const lng = typeof body.lng === "number" ? body.lng : Number(body.lng);

  if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
    return null;
  }

  if (lat < -90 || lat > 90 || lng < -180 || lng > 180) {
    return null;
  }

  return { lat, lng };
}

export async function POST(request: Request) {
  let body: GeoBody;

  try {
    body = (await request.json()) as GeoBody;
  } catch {
    return NextResponse.json({ error: "Invalid JSON body." }, { status: 400 });
  }

  const coordinates = parseBody(body);
  if (!coordinates) {
    return NextResponse.json(
      { error: "lat and lng must be valid coordinates." },
      { status: 400 },
    );
  }

  const response = NextResponse.json({ ok: true });
  response.cookies.set(
    COORDS_COOKIE_NAME,
    formatCoordinatePair(coordinates),
    {
      path: "/",
      maxAge: 60 * 60 * 24,
      sameSite: "lax",
    },
  );

  return response;
}
