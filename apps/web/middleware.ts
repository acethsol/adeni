import type { NextRequest } from "next/server";
import { runAuthMiddleware } from "./lib/auth/middleware";
import { applyMarketCookie } from "./lib/market-middleware";

export async function middleware(request: NextRequest) {
  const response = await runAuthMiddleware(request);
  return applyMarketCookie(request, response);
}

export const config = {
  matcher: [
    "/((?!_next/static|_next/image|favicon.ico|sitemap.xml|robots.txt).*)",
  ],
};
