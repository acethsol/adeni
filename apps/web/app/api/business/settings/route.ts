import { NextResponse } from "next/server";
import { updateBusinessSettingsRequestSchema } from "@adeni/shared";
import { createBusinessApiClient } from "@/lib/business-api";

export async function PATCH(request: Request) {
  try {
    const body = updateBusinessSettingsRequestSchema.parse(await request.json());
    const client = await createBusinessApiClient();
    const profile = await client.updateTenantSettings(body);
    return NextResponse.json(profile);
  } catch (error) {
    const message = error instanceof Error ? error.message : "Could not update settings.";
    return NextResponse.json({ title: message }, { status: 400 });
  }
}
