import { NextResponse } from "next/server";
import { availableSlotsResponseSchema } from "@adeni/shared";
import { createApiClient } from "@/lib/adeni";

type Props = {
  params: Promise<{ slug: string }>;
};

export async function GET(request: Request, { params }: Props) {
  const { slug } = await params;
  const { searchParams } = new URL(request.url);
  const serviceId = searchParams.get("serviceId");
  const from = searchParams.get("from");
  const to = searchParams.get("to");

  if (!serviceId || !from || !to) {
    return NextResponse.json(
      { title: "serviceId, from, and to are required." },
      { status: 400 },
    );
  }

  try {
    const client = createApiClient();
    const items = await client.getBusinessSlots(slug, { serviceId, from, to });
    const payload = availableSlotsResponseSchema.parse({ items });
    return NextResponse.json(payload);
  } catch {
    return NextResponse.json({ title: "Could not load slots." }, { status: 502 });
  }
}
