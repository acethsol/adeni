import { NextResponse } from "next/server";
import {
  messagingSettingsSchema,
  updateMessagingSettingsRequestSchema,
} from "@adeni/shared";
import { createBusinessApiClient } from "@/lib/business-api";

export async function GET() {
  try {
    const client = await createBusinessApiClient();
    const settings = await client.getMessagingSettings();
    const parsed = messagingSettingsSchema.safeParse(settings);
    if (!parsed.success) {
      return NextResponse.json({ title: "Invalid messaging settings response." }, { status: 502 });
    }

    return NextResponse.json(parsed.data);
  } catch {
    return NextResponse.json({ title: "Could not load messaging settings." }, { status: 502 });
  }
}

export async function PATCH(request: Request) {
  try {
    const body = updateMessagingSettingsRequestSchema.parse(await request.json());
    const client = await createBusinessApiClient();
    const settings = await client.updateMessagingSettings(body);
    return NextResponse.json(settings);
  } catch (error) {
    const message = error instanceof Error ? error.message : "Could not update messaging settings.";
    return NextResponse.json({ title: message }, { status: 400 });
  }
}
