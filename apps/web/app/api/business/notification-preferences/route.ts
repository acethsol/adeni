import { NextResponse } from "next/server";
import {
  notificationPreferencesSchema,
  updateNotificationPreferencesRequestSchema,
} from "@adeni/shared";
import { createBusinessApiClient } from "@/lib/business-api";

export async function GET() {
  try {
    const client = await createBusinessApiClient();
    const prefs = await client.getNotificationPreferences();
    const parsed = notificationPreferencesSchema.safeParse(prefs);
    if (!parsed.success) {
      return NextResponse.json({ title: "Invalid notification preferences response." }, { status: 502 });
    }

    return NextResponse.json(parsed.data);
  } catch {
    return NextResponse.json({ title: "Could not load notification preferences." }, { status: 502 });
  }
}

export async function PATCH(request: Request) {
  try {
    const body = updateNotificationPreferencesRequestSchema.parse(await request.json());
    const client = await createBusinessApiClient();
    const prefs = await client.updateNotificationPreferences(body);
    return NextResponse.json(prefs);
  } catch (error) {
    const message = error instanceof Error ? error.message : "Could not update notification preferences.";
    return NextResponse.json({ title: message }, { status: 400 });
  }
}
