import { NextResponse } from "next/server";
import { whatsAppLinkResponseSchema } from "@adeni/shared";
import { createApiClient } from "@/lib/adeni";

type Params = { params: Promise<{ id: string }> };

export async function GET(_request: Request, { params }: Params) {
  const { id } = await params;

  try {
    const client = await createApiClient();
    const link = await client.getBookingWhatsAppLink(id);
    const parsed = whatsAppLinkResponseSchema.safeParse(link);
    if (!parsed.success) {
      return NextResponse.json({ title: "Invalid WhatsApp link." }, { status: 502 });
    }

    return NextResponse.json(parsed.data);
  } catch {
    return NextResponse.json({ title: "Could not build WhatsApp link." }, { status: 502 });
  }
}
