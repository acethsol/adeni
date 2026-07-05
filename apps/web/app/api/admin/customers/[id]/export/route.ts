import { NextResponse } from "next/server";
import { customerDataExportSchema } from "@adeni/shared";
import { createAuthenticatedApiClient } from "@/lib/adeni";

type Params = { params: Promise<{ id: string }> };

export async function GET(_request: Request, { params }: Params) {
  const { id } = await params;

  try {
    const client = await createAuthenticatedApiClient();
    const payload = await client.exportAdminCustomer(id);
    const parsed = customerDataExportSchema.safeParse(payload);
    if (!parsed.success) {
      return NextResponse.json({ title: "Invalid export response." }, { status: 502 });
    }
    return NextResponse.json(parsed.data);
  } catch {
    return NextResponse.json({ title: "Could not export customer." }, { status: 502 });
  }
}
