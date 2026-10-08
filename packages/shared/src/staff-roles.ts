export const STAFF_ROLE_KEYS = [
  "stylist",
  "barber",
  "nail_tech",
  "esthetician",
  "therapist",
  "receptionist",
  "other",
] as const;

export type StaffRoleKey = (typeof STAFF_ROLE_KEYS)[number];

export const STAFF_ROLE_LABELS: Record<StaffRoleKey, string> = {
  stylist: "Stylist",
  barber: "Barber",
  nail_tech: "Nail tech",
  esthetician: "Esthetician",
  therapist: "Therapist",
  receptionist: "Receptionist",
  other: "Other",
};

export function isStaffRoleKey(value: string): value is StaffRoleKey {
  return (STAFF_ROLE_KEYS as readonly string[]).includes(value);
}
