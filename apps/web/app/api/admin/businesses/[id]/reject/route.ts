import { NextResponse } from "next/server";
import { rejectBusinessRequestSchema } from "@adeni/shared";
import { createAuthenticatedApiClient } from "@/lib/adeni";

type Params = { params: Promise<{ id: string }> };

export async function POST(request: Request, { params }: Params) {
  const { id } = await params;
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Invalid JSON body." }, { status: 400 });
  }

  const parsed = rejectBusinessRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ title: "Rejection reason must be at least 10 characters." }, { status: 400 });
  }

  try {
    const client = await createAuthenticatedApiClient();
    await client.rejectPendingBusiness(id, parsed.data.reason);
    return new NextResponse(null, { status: 204 });
  } catch {
    return NextResponse.json({ title: "Could not reject business." }, { status: 502 });
  }
}
