import { describe, expect, it } from "vitest";
import { customerActivitySchema, customerProfileSchema } from "./customer-profile";
import { t } from "./i18n";

const availability = { booking: "available", finance: "restricted", messaging: "unscoped", preferences: "notImplemented", retention: "notImplemented" };

describe("customer profile contracts", () => {
  it("distinguishes unavailable data from an empty or zero balance", () => {
    const profile = customerProfileSchema.parse({ tenantCustomerId: "roster", asOf: "2026-10-25", locationId: null,
      availability, visitSummary: null, nextAppointment: null, recordedOnlinePaymentsByCurrency: null });
    expect(profile.recordedOnlinePaymentsByCurrency).toBeNull();
    expect(profile.availability.retention).toBe("notImplemented");
    expect(customerProfileSchema.safeParse({ ...profile, recordedOnlinePaymentsByCurrency: [{ currency: "CAD", amount: "100" }] }).success).toBe(false);
    expect(customerProfileSchema.safeParse({ ...profile, visitSummary: { completedBookings: -1, completedWalkIns: 0 } }).success).toBe(false);
  });

  it("validates current next-appointment types without inventing retention fields", () => {
    const profile = { tenantCustomerId: "roster", asOf: "2026-10-25", locationId: null, availability,
      visitSummary: { completedBookings: 2, completedWalkIns: 1 }, recordedOnlinePaymentsByCurrency: [{ currency: "CAD", amount: 12.50 }],
      nextAppointment: { bookingId: "booking", startAt: "2026-10-26", serviceName: "Éclat", status: "Pending" } };
    expect(customerProfileSchema.parse(profile).nextAppointment?.serviceName).toBe("Éclat");
    expect(customerProfileSchema.safeParse({ ...profile, nextAppointment: { ...profile.nextAppointment, status: "Cancelled" } }).success).toBe(false);
  });

  it("validates bounded activity metadata and rejects unsupported event kinds", () => {
    const response = { items: [{ id: "message:id", kind: "messageSent", occurredAt: "2026-10-25", source: "messaging", sourceId: "conversation",
      locationId: null, serviceName: null, currentStatus: null, amount: null, currency: null }],
      nextCursor: "protected cursor", asOf: "2026-10-25", from: "2026-07-27", isTruncated: true, availability };
    expect(customerActivitySchema.parse(response).isTruncated).toBe(true);
    expect(customerActivitySchema.safeParse({ ...response, items: [{ ...response.items[0], kind: "membershipCreated" }] }).success).toBe(false);
  });

  it("has localized labels for each activity kind and source status", () => {
    const keys = ["bookingCreated", "appointmentScheduled", "queueJoined", "queueCompleted", "queueNoShow", "onlinePaymentRecorded", "messageSent"];
    for (const locale of ["en", "fr"] as const) {
      for (const kind of keys) expect(t(locale, `customerProfile.kinds.${kind}`)).not.toBe(`customerProfile.kinds.${kind}`);
      expect(t(locale, "customerProfile.paymentCoverage")).not.toBe("customerProfile.paymentCoverage");
    }
  });
});
