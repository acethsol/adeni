import { describe, expect, it } from "vitest";
import { customerSegmentsSchema, customerSegmentsBatchRequestSchema, type CustomerSegments } from "./customer-segments";
import { customerInsightsCopy, customerSegmentReason } from "./customer-insights-copy";
import { customerSegmentsSchema as publicSchema } from "./index";
const id = "90000000-0000-4000-8000-000000000001";
const value = { tenantCustomerId: id, availability: "available", asOf: "2026-10-26T12:00:00Z", definitionVersion: "v1", locationId: null, timeZoneId: "Africa/Lagos", approximateVisitDates: false,
  matches: [{ id: "vip", count: 6, daysSinceLastVisit: 1 }] };
describe("customer segments contracts", () => {
  it("preserves the shared public export and explicit scope/reasons", () => {
    const parsed: CustomerSegments = publicSchema.parse(value);
    expect(parsed.matches[0]?.id).toBe("vip"); expect(parsed.locationId).toBeNull();
  });
  it("rejects unsupported labels, unknown versions and misleading counts", () => {
    for (const change of [{ definitionVersion: "v2" }, { availability: "false" }, { matches: [{ id: "activeMember", count: 0, daysSinceLastVisit: null }] }, { matches: [{ id: "vip", count: -1, daysSinceLastVisit: 1 }] }])
      expect(customerSegmentsSchema.safeParse({ ...value, ...change }).success).toBe(false);
  });
  it("bounds batches and rejects duplicates and missing fields", () => {
    expect(customerSegmentsBatchRequestSchema.safeParse({ customerIds: [id] }).success).toBe(true);
    for (const ids of [[], [id, id], Array(101).fill(id)]) expect(customerSegmentsBatchRequestSchema.safeParse({ customerIds: ids }).success).toBe(false);
    expect(customerSegmentsSchema.safeParse({ ...value, timeZoneId: undefined }).success).toBe(false);
  });
  it("provides English/French explanations without money or general appointment-no-show claims", () => {
    expect(customerInsightsCopy("en", "vip")).toContain("frequent visitor");
    expect(customerInsightsCopy("fr", "frequentQueueNoShow")).toContain("file d’attente");
    expect(customerSegmentReason("en", "frequentQueueNoShow", 2, null)).toContain("2 recorded queue no-shows");
    expect(customerSegmentReason("fr", "lapsed", 3, 100)).toContain("100");
  });
});
