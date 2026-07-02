import type { NextRequest } from "next/server";
import { runAuthMiddleware } from "./lib/auth/middleware";

export async function middleware(request: NextRequest) {
  return runAuthMiddleware(request);
}

export const config = {
  matcher: [
    "/((?!_next/static|_next/image|favicon.ico|sitemap.xml|robots.txt).*)",
  ],
};
