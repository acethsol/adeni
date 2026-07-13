import { NextResponse } from "next/server";
import { createAuthenticatedApiClient } from "@/lib/adeni";

type Params = {
  params: Promise<{ id: string }>;
};

export async function PATCH(request: Request, { params }: Params) {
  try {
    const { id } = await params;
    const client = await createAuthenticatedApiClient();
    const body = (await request.json()) as { isLive?: boolean };
    await client.setAdminMarketLive(id, Boolean(body.isLive));
    return new NextResponse(null, { status: 204 });
  } catch {
    return NextResponse.json({ title: "Unauthorized" }, { status: 401 });
  }
}
