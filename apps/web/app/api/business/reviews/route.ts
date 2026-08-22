import { NextResponse } from "next/server";
import { createBusinessApiClient } from "@/lib/business-api";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const page = Number(searchParams.get("page") ?? "1") || 1;
  const pageSize = Number(searchParams.get("pageSize") ?? "10") || 10;

  try {
    const client = await createBusinessApiClient();
    const profile = await client.getTenantProfile();
    const primaryLocation = profile.locations.find((item) => item.isPrimary) ?? profile.locations[0];

    if (!primaryLocation) {
      return NextResponse.json({ ratingAvg: null, reviewCount: 0, items: [], page, pageSize, totalCount: 0 });
    }

    const [publicProfile, reviews] = await Promise.all([
      client.getBusinessProfile(primaryLocation.slug),
      client.getBusinessReviews(primaryLocation.slug, page, pageSize),
    ]);

    return NextResponse.json({
      ratingAvg: publicProfile.ratingAvg ?? null,
      reviewCount: publicProfile.reviewCount ?? 0,
      locationName: primaryLocation.name,
      ...reviews,
    });
  } catch {
    return NextResponse.json({ title: "Could not load reviews." }, { status: 502 });
  }
}
