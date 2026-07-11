import { NextResponse, type NextRequest } from "next/server";
import { getMarketById } from "@adeni/shared";
import { MARKET_COOKIE_NAME } from "@/lib/market-constants";

export function applyMarketCookie(request: NextRequest, response: NextResponse): NextResponse {
  const queryMarket = request.nextUrl.searchParams.get("market")?.trim().toLowerCase();
  if (!queryMarket || !getMarketById(queryMarket)) {
    return response;
  }

  response.cookies.set(MARKET_COOKIE_NAME, queryMarket, {
    path: "/",
    maxAge: 60 * 60 * 24 * 365,
    sameSite: "lax",
  });

  return response;
}
