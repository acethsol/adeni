import {
  hasBenefitsSnapshot,
  pickHomeConversations,
  pickReviewCandidate,
  summarizeBenefits,
} from "./customer-dashboard.helpers";
import type {
  BusinessGiftCard,
  ConversationSummary,
  CustomerBookingResponse,
  CustomerPackage,
} from "@adeni/shared";

function booking(partial: Partial<CustomerBookingResponse>): CustomerBookingResponse {
  return {
    id: "b1",
    tenantId: "t1",
    businessName: "Cuts",
    businessSlug: "cuts",
    serviceOfferingId: "s1",
    serviceName: "Fade",
    startAt: "2026-01-01T10:00:00.000Z",
    endAt: "2026-01-01T10:30:00.000Z",
    status: 1,
    createdAt: "2026-01-01T09:00:00.000Z",
    guestCount: 1,
    ...partial,
  };
}

describe("customer-dashboard helpers (C2)", () => {
  it("picks the newest reviewable booking", () => {
    const now = Date.parse("2026-06-01T00:00:00.000Z");
    const pick = pickReviewCandidate(
      [
        booking({
          id: "old",
          startAt: "2026-02-01T10:00:00.000Z",
          canReview: true,
          hasReview: false,
        }),
        booking({
          id: "new",
          startAt: "2026-05-01T10:00:00.000Z",
          canReview: true,
          hasReview: false,
        }),
        booking({
          id: "done",
          startAt: "2026-05-15T10:00:00.000Z",
          canReview: true,
          hasReview: true,
        }),
      ],
      now,
    );
    expect(pick?.id).toBe("new");
  });

  it("prioritizes conversations that need attention", () => {
    const rows: ConversationSummary[] = [
      {
        id: "11111111-1111-1111-1111-111111111111",
        tenantId: "22222222-2222-2222-2222-222222222222",
        contextType: 0,
        customerDisplayName: "You",
        businessName: "Quiet",
        preview: "ok",
        lastMessageAt: "2026-06-02T00:00:00.000Z",
        needsAttention: false,
      },
      {
        id: "33333333-3333-3333-3333-333333333333",
        tenantId: "22222222-2222-2222-2222-222222222222",
        contextType: 0,
        customerDisplayName: "You",
        businessName: "Busy",
        preview: "hi",
        lastMessageAt: "2026-06-01T00:00:00.000Z",
        needsAttention: true,
      },
    ];
    expect(pickHomeConversations(rows, 1)[0]?.businessName).toBe("Busy");
  });

  it("summarizes benefits without mixing gift-card currencies", () => {
    const packages = [
      {
        id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        tenantId: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
        tenantCustomerId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
        packageDefinitionId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
        name: "Pack",
        listPrice: 0,
        currency: "NGN",
        status: "active",
        soldChannel: "online",
        soldAtLocationId: null,
        redemptionScope: "all_locations",
        redemptionLocationIds: [],
        activatedAt: null,
        expiresAt: "2026-12-01T00:00:00.000Z",
        voidedAt: null,
        voidReason: null,
        note: null,
        balances: [
          {
            id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
            serviceOfferingId: "ffffffff-ffff-ffff-ffff-ffffffffffff",
            serviceName: "Cut",
            granted: 5,
            used: 2,
            reserved: 0,
            forfeited: 0,
            remaining: 3,
            expiresAt: "2026-11-01T00:00:00.000Z",
          },
        ],
      },
    ] as CustomerPackage[];

    const gifts = [
      {
        id: "11111111-1111-1111-1111-111111111111",
        businessName: "A",
        originalAmount: 100,
        remainingAmount: 40,
        currency: "NGN",
        state: "Active",
        codeSuffix: "ABCD",
        issuedAt: null,
      },
      {
        id: "22222222-2222-2222-2222-222222222222",
        businessName: "B",
        originalAmount: 50,
        remainingAmount: 20,
        currency: "CAD",
        state: "Active",
        codeSuffix: "EFGH",
        issuedAt: null,
      },
    ] as BusinessGiftCard[];

    const snap = summarizeBenefits(packages, gifts);
    expect(snap.activePackages).toBe(1);
    expect(snap.creditsRemaining).toBe(3);
    expect(snap.soonestExpiry).toBe("2026-11-01T00:00:00.000Z");
    expect(snap.giftBalancesByCurrency).toEqual([
      { currency: "CAD", amount: 20 },
      { currency: "NGN", amount: 40 },
    ]);
    expect(hasBenefitsSnapshot(snap)).toBe(true);
  });
});
