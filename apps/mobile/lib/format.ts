import {
  formatPrice,
  formatSlotTime,
  slotRange,
} from "@adeni/shared";

export { formatPrice, formatSlotTime, slotRange };

const TENANT_STATUS_LABELS: Record<number, string> = {
  0: "Draft",
  1: "Pending verification",
  2: "Verified",
  3: "Rejected",
  4: "Suspended",
};

export function formatTenantStatus(status: number): string {
  return TENANT_STATUS_LABELS[status] ?? "Unknown";
}

const BOOKING_STATUS_LABELS: Record<number, string> = {
  0: "Pending",
  1: "Confirmed",
  2: "Rejected",
  3: "Cancelled",
};

export function formatBookingStatus(status: number): string {
  return BOOKING_STATUS_LABELS[status] ?? "Unknown";
}
