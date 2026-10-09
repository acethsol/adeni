/**
 * Staff role keys for the owner-managed roster.
 *
 * Category-specific keys = bookable practitioners (shown by business category).
 * Shared keys ≈ Zenoti-style center ops roles (Accountant, Manager, …) — always available.
 * See Zenoti “Default security roles”: receptionist, accountant, inventory manager,
 * supervisor, marketing manager, HR manager, manager, admin staff, owner/admin
 * (owner/admin omitted — portal Auth0 owner covers that).
 */
export const STAFF_ROLE_KEYS = [
  // Practitioners (category-scoped in the UI)
  "stylist",
  "barber",
  "nail_tech",
  "esthetician",
  "therapist",
  "instructor",
  "trainer",
  // Center ops (category-agnostic; Zenoti-aligned)
  "receptionist",
  "supervisor",
  "manager",
  "accountant",
  "inventory_manager",
  "marketing",
  "hr",
  "admin_staff",
  "other",
] as const;

export type StaffRoleKey = (typeof STAFF_ROLE_KEYS)[number];

export const STAFF_ROLE_LABELS: Record<StaffRoleKey, string> = {
  stylist: "Stylist",
  barber: "Barber",
  nail_tech: "Nail tech",
  esthetician: "Esthetician",
  therapist: "Therapist",
  instructor: "Instructor",
  trainer: "Trainer",
  receptionist: "Receptionist",
  supervisor: "Supervisor",
  manager: "Manager",
  accountant: "Accountant",
  inventory_manager: "Inventory manager",
  marketing: "Marketing",
  hr: "HR",
  admin_staff: "Admin / ops",
  other: "Other",
};

/** Always offered regardless of category (Zenoti-style ops roles). */
export const SHARED_STAFF_ROLES: readonly StaffRoleKey[] = [
  "receptionist",
  "supervisor",
  "manager",
  "accountant",
  "inventory_manager",
  "marketing",
  "hr",
  "admin_staff",
  "other",
];

/**
 * Primary practitioner role options per wellness category slug.
 * Multi-category businesses get the union of their categories + shared ops roles.
 */
const CATEGORY_STAFF_ROLES: Record<string, readonly StaffRoleKey[]> = {
  "hair-grooming": ["stylist", "barber"],
  barbers: ["stylist", "barber"],
  "hair-salons": ["stylist", "barber"],
  nails: ["nail_tech"],
  "nail-spa": ["nail_tech"],
  "skincare-aesthetics": ["esthetician", "stylist"],
  "makeup-brows": ["esthetician", "stylist"],
  "spa-relaxation": ["therapist", "esthetician"],
  "massage-bodywork": ["therapist"],
  fitness: ["trainer", "instructor"],
  yoga: ["instructor"],
  pilates: ["instructor"],
  "recovery-performance": ["therapist", "trainer"],
  "holistic-wellness": ["therapist", "instructor"],
};

const FALLBACK_PRACTITIONER_ROLES: readonly StaffRoleKey[] = [
  "stylist",
  "barber",
  "nail_tech",
  "esthetician",
  "therapist",
  "instructor",
  "trainer",
];

export function isStaffRoleKey(value: string): value is StaffRoleKey {
  return (STAFF_ROLE_KEYS as readonly string[]).includes(value);
}

function practitionerRolesForCategory(categorySlug: string): StaffRoleKey[] {
  const normalized = categorySlug.trim().toLowerCase();
  return [...(CATEGORY_STAFF_ROLES[normalized] ?? FALLBACK_PRACTITIONER_ROLES)];
}

/** Union of practitioner roles for categories + shared ops roles. */
export function staffRolesForCategories(
  categorySlugs: readonly string[],
  /** Keep a saved role visible even if the category set no longer includes it. */
  preserveRoleKey?: string | null,
): StaffRoleKey[] {
  const slugs = categorySlugs.map((s) => s.trim().toLowerCase()).filter(Boolean);
  const ordered: StaffRoleKey[] = [];
  const seen = new Set<StaffRoleKey>();

  const add = (key: StaffRoleKey) => {
    if (seen.has(key)) return;
    seen.add(key);
    ordered.push(key);
  };

  if (slugs.length === 0) {
    for (const key of FALLBACK_PRACTITIONER_ROLES) {
      add(key);
    }
  } else {
    for (const slug of slugs) {
      for (const key of practitionerRolesForCategory(slug)) {
        add(key);
      }
    }
  }

  if (preserveRoleKey && isStaffRoleKey(preserveRoleKey) && !seen.has(preserveRoleKey)) {
    add(preserveRoleKey);
  }

  for (const key of SHARED_STAFF_ROLES) {
    add(key);
  }

  return ordered;
}

export function defaultStaffRoleForCategories(categorySlugs: readonly string[]): StaffRoleKey {
  const roles = staffRolesForCategories(categorySlugs);
  const shared = new Set(SHARED_STAFF_ROLES);
  return roles.find((r) => !shared.has(r)) ?? "receptionist";
}
