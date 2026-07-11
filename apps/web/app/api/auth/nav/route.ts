import { NextResponse } from "next/server";
import { getOptionalSession } from "@/lib/auth/session";
import { isAuth0Configured } from "@/lib/auth/config";

export async function GET() {
  const configured = isAuth0Configured();

  if (!configured) {
    return NextResponse.json({ configured: false, session: null });
  }

  const session = await getOptionalSession();

  return NextResponse.json({
    configured: true,
    session: session
      ? { name: session.name, email: session.email }
      : null,
  });
}
