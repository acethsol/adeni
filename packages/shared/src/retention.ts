import { z } from "zod";

/** Retention (R1-A): prepaid service packages + benefit ledger. Credits are service/session counts — never currency. */

export const packageRedemptionScopeSchema = z.enum(["all_locations", "purchase_location_only", "selected_locations"]);
export const packageSellScopeSchema = z.enum(["all_locations", "selected_locations"]);
export const customerPackageStatusSchema = z.enum(["pending_payment", "active", "expired", "voided"]);
export const packageSoldChannelSchema = z.enum(["in_person_till", "in_person_transfer", "online"]);
export const sellPaymentMethodSchema = z.enum(["till", "transfer"]);
export const benefitTransactionKindSchema = z.enum(["grant", "reserve", "release", "redeem", "forfeit", "void", "adjust"]);

export const packageBenefitSchema = z.object({
  id: z.string().uuid(),
  serviceOfferingId: z.string().uuid(),
  serviceName: z.string(),
  quantityGranted: z.number().int().positive(),
});

export const packageDefinitionSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  description: z.string().nullable(),
  listPrice: z.number().nonnegative(),
  currency: z.string(),
  isActive: z.boolean(),
  defaultExpiryMonths: z.number().int().positive().nullable(),
  effectiveExpiryMonths: z.number().int().positive(),
  redemptionScope: packageRedemptionScopeSchema,
  sellScope: packageSellScopeSchema,
  redemptionLocationIds: z.array(z.string().uuid()),
  sellLocationIds: z.array(z.string().uuid()),
  benefits: z.array(packageBenefitSchema),
  createdAt: z.string(),
  updatedAt: z.string(),
});

export const packageDefinitionListSchema = z.object({ items: z.array(packageDefinitionSchema) });

export const packageBenefitInputSchema = z.object({
  serviceOfferingId: z.string().uuid(),
  quantityGranted: z.number().int().min(1).max(100),
});

export const createPackageDefinitionRequestSchema = z.object({
  name: z.string().min(1).max(120),
  description: z.string().max(500).optional(),
  listPrice: z.number().nonnegative(),
  currency: z.string().length(3),
  defaultExpiryMonths: z.number().int().min(1).max(60).optional(),
  redemptionScope: packageRedemptionScopeSchema.optional(),
  sellScope: packageSellScopeSchema.optional(),
  redemptionLocationIds: z.array(z.string().uuid()).optional(),
  sellLocationIds: z.array(z.string().uuid()).optional(),
  benefits: z.array(packageBenefitInputSchema).min(1).max(10),
});

export const updatePackageDefinitionRequestSchema = createPackageDefinitionRequestSchema.partial();

export const benefitBalanceSchema = z.object({
  id: z.string().uuid(),
  serviceOfferingId: z.string().uuid(),
  serviceName: z.string(),
  granted: z.number().int().nonnegative(),
  used: z.number().int().nonnegative(),
  reserved: z.number().int().nonnegative(),
  forfeited: z.number().int().nonnegative(),
  remaining: z.number().int().nonnegative(),
  expiresAt: z.string().nullable(),
});

export const customerPackageSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  tenantCustomerId: z.string().uuid(),
  packageDefinitionId: z.string().uuid(),
  name: z.string(),
  listPrice: z.number().nonnegative(),
  currency: z.string(),
  status: customerPackageStatusSchema,
  soldChannel: packageSoldChannelSchema,
  soldAtLocationId: z.string().uuid().nullable(),
  redemptionScope: packageRedemptionScopeSchema,
  redemptionLocationIds: z.array(z.string().uuid()),
  activatedAt: z.string().nullable(),
  expiresAt: z.string().nullable(),
  voidedAt: z.string().nullable(),
  voidReason: z.string().nullable(),
  note: z.string().nullable(),
  balances: z.array(benefitBalanceSchema),
  businessName: z.string().nullable().optional(),
});

export const customerPackageListSchema = z.object({ items: z.array(customerPackageSchema) });

export const sellPackageRequestSchema = z.object({
  packageDefinitionId: z.string().uuid(),
  businessLocationId: z.string().uuid(),
  paymentMethod: sellPaymentMethodSchema,
  note: z.string().max(500).optional(),
});

export const voidCustomerPackageRequestSchema = z.object({ reason: z.string().max(500).optional() });

export const benefitTransactionSchema = z.object({
  id: z.string().uuid(),
  sequence: z.number().int(),
  kind: z.string(),
  quantity: z.number().int(),
  delta: z.number().int(),
  reservedAfter: z.number().int(),
  usedAfter: z.number().int(),
  remainingAfter: z.number().int(),
  benefitBalanceId: z.string().uuid(),
  bookingId: z.string().uuid().nullable(),
  businessLocationId: z.string().uuid().nullable(),
  reason: z.string().nullable(),
  occurredAt: z.string(),
});

export const customerPackageLedgerSchema = z.object({ items: z.array(benefitTransactionSchema) });

export const redeemableBenefitSchema = z.object({
  customerPackageId: z.string().uuid(),
  packageName: z.string(),
  benefitBalanceId: z.string().uuid(),
  serviceOfferingId: z.string().uuid(),
  serviceName: z.string(),
  remaining: z.number().int().nonnegative(),
  expiresAt: z.string().nullable(),
});

export const redeemableBenefitListSchema = z.object({ items: z.array(redeemableBenefitSchema) });

/** Booking payload: apply package credits to a booking (one credit = one service unit). */
export const appliedBenefitRequestSchema = z.object({
  customerPackageId: z.string().uuid(),
  benefitBalanceId: z.string().uuid(),
  quantity: z.number().int().min(1).max(10).optional(),
});

/** F1 customer profile summary — counts and names only. */
export const customerPackageSummarySchema = z.object({
  activeCount: z.number().int().nonnegative(),
  totalRemainingCredits: z.number().int().nonnegative(),
  activePackageNames: z.array(z.string()),
});

/** Discover storefront (anonymous catalog). `onlinePurchaseEnabled` is false until merchant settlement is live (Sprint 34 gate). */
export const publicPackageSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  description: z.string().nullable(),
  listPrice: z.number().nonnegative(),
  currency: z.string(),
  expiryMonths: z.number().int().positive(),
  redemptionScope: packageRedemptionScopeSchema,
  benefits: z.array(packageBenefitSchema),
});

export const publicPackageCatalogSchema = z.object({
  items: z.array(publicPackageSchema),
  onlinePurchaseEnabled: z.boolean(),
});

/** Online purchase start: package stays `pending_payment` until PaymentCompleted. */
export const packagePurchaseResponseSchema = z.object({
  customerPackageId: z.string().uuid(),
  paymentIntentId: z.string().uuid(),
  status: customerPackageStatusSchema,
  checkoutUrl: z.string().nullable(),
});

export type PublicPackage = z.infer<typeof publicPackageSchema>;
export type PublicPackageCatalog = z.infer<typeof publicPackageCatalogSchema>;
export type PackagePurchaseResponse = z.infer<typeof packagePurchaseResponseSchema>;
export type PackageDefinition = z.infer<typeof packageDefinitionSchema>;
export type CreatePackageDefinitionRequest = z.infer<typeof createPackageDefinitionRequestSchema>;
export type UpdatePackageDefinitionRequest = z.infer<typeof updatePackageDefinitionRequestSchema>;
export type CustomerPackage = z.infer<typeof customerPackageSchema>;
export type BenefitBalance = z.infer<typeof benefitBalanceSchema>;
export type SellPackageRequest = z.infer<typeof sellPackageRequestSchema>;
export type BenefitTransaction = z.infer<typeof benefitTransactionSchema>;
export type RedeemableBenefit = z.infer<typeof redeemableBenefitSchema>;
export type AppliedBenefitRequest = z.infer<typeof appliedBenefitRequestSchema>;
export type CustomerPackageSummary = z.infer<typeof customerPackageSummarySchema>;

// ---- Memberships (Sprint 37.2-A): plan catalog. Enrolment / billing land in later slices. ----

export const membershipBillingIntervalSchema = z.enum(["monthly", "quarterly", "annually"]);
export const membershipRolloverPolicySchema = z.enum(["none", "carry_over_one_cycle"]);
export const membershipCancellationPolicySchema = z.enum(["end_of_cycle", "immediate"]);

export const membershipBenefitSchema = z.object({
  id: z.string().uuid(),
  serviceOfferingId: z.string().uuid(),
  serviceName: z.string(),
  quantityPerCycle: z.number().int().positive(),
});

export const membershipPlanSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  description: z.string().nullable(),
  price: z.number().positive(),
  currency: z.string(),
  billingInterval: membershipBillingIntervalSchema,
  rolloverPolicy: membershipRolloverPolicySchema,
  cancellationPolicy: membershipCancellationPolicySchema,
  allowFreeze: z.boolean(),
  pastDueGraceDays: z.number().int().min(0).max(30),
  isActive: z.boolean(),
  benefits: z.array(membershipBenefitSchema),
  createdAt: z.string(),
  updatedAt: z.string(),
});

export const membershipPlanListSchema = z.object({ items: z.array(membershipPlanSchema) });

export const membershipBenefitInputSchema = z.object({
  serviceOfferingId: z.string().uuid(),
  quantityPerCycle: z.number().int().min(1).max(1000),
});

export const createMembershipPlanRequestSchema = z.object({
  name: z.string().min(1).max(200),
  description: z.string().max(1000).optional(),
  price: z.number().positive(),
  currency: z.string().length(3),
  billingInterval: membershipBillingIntervalSchema.optional(),
  rolloverPolicy: membershipRolloverPolicySchema.optional(),
  cancellationPolicy: membershipCancellationPolicySchema.optional(),
  allowFreeze: z.boolean().optional(),
  pastDueGraceDays: z.number().int().min(0).max(30).optional(),
  benefits: z.array(membershipBenefitInputSchema).min(1).max(10),
});

export const updateMembershipPlanRequestSchema = createMembershipPlanRequestSchema.partial();

export type MembershipPlan = z.infer<typeof membershipPlanSchema>;
export type MembershipPlanBenefit = z.infer<typeof membershipBenefitSchema>;
export type CreateMembershipPlanRequest = z.infer<typeof createMembershipPlanRequestSchema>;
export type UpdateMembershipPlanRequest = z.infer<typeof updateMembershipPlanRequestSchema>;
export type MembershipBillingInterval = z.infer<typeof membershipBillingIntervalSchema>;

// ---- Memberships (Sprint 37.2-B): customer memberships, in-person enrolment. ----

export const customerMembershipStatusSchema = z.enum([
  "pending",
  "active",
  "past_due",
  "frozen",
  "cancel_pending",
  "cancelled",
  "expired",
]);
export const membershipEnrolChannelSchema = z.enum(["in_person_till", "in_person_transfer", "online"]);

export const membershipBenefitSnapshotSchema = z.object({
  serviceOfferingId: z.string().uuid(),
  serviceName: z.string(),
  quantityPerCycle: z.number().int().positive(),
});

/** Credits of the membership's current cycle only; service sessions, never money. */
export const membershipCycleBalanceSchema = z.object({
  id: z.string().uuid(),
  cycleNumber: z.number().int().positive(),
  serviceOfferingId: z.string().uuid(),
  serviceName: z.string(),
  granted: z.number().int().nonnegative(),
  used: z.number().int().nonnegative(),
  reserved: z.number().int().nonnegative(),
  forfeited: z.number().int().nonnegative(),
  remaining: z.number().int().nonnegative(),
  expiresAt: z.string().nullable(),
});

export const customerMembershipSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  tenantCustomerId: z.string().uuid(),
  membershipPlanId: z.string().uuid(),
  name: z.string(),
  price: z.number().nonnegative(),
  currency: z.string(),
  billingInterval: membershipBillingIntervalSchema,
  rolloverPolicy: membershipRolloverPolicySchema,
  cancellationPolicy: membershipCancellationPolicySchema,
  allowFreeze: z.boolean(),
  pastDueGraceDays: z.number().int().nonnegative(),
  status: customerMembershipStatusSchema,
  enrolChannel: membershipEnrolChannelSchema,
  enrolledAtLocationId: z.string().uuid().nullable(),
  currentCycleNumber: z.number().int().nonnegative(),
  currentCycleStart: z.string().nullable(),
  currentCycleEnd: z.string().nullable(),
  activatedAt: z.string().nullable(),
  frozenAt: z.string().nullable(),
  cancelRequestedAt: z.string().nullable(),
  cancelledAt: z.string().nullable(),
  cancelReason: z.string().nullable(),
  note: z.string().nullable(),
  benefits: z.array(membershipBenefitSnapshotSchema),
  balances: z.array(membershipCycleBalanceSchema),
  createdAt: z.string(),
});

export const customerMembershipListSchema = z.object({ items: z.array(customerMembershipSchema) });

export const enrolMembershipRequestSchema = z.object({
  membershipPlanId: z.string().uuid(),
  businessLocationId: z.string().uuid(),
  paymentMethod: sellPaymentMethodSchema,
  note: z.string().max(500).optional(),
});

export const cancelMembershipRequestSchema = z.object({ reason: z.string().max(500).optional() });

export type CustomerMembership = z.infer<typeof customerMembershipSchema>;
export type CustomerMembershipStatus = z.infer<typeof customerMembershipStatusSchema>;
export type MembershipCycleBalance = z.infer<typeof membershipCycleBalanceSchema>;
export type EnrolMembershipRequest = z.infer<typeof enrolMembershipRequestSchema>;
export type CancelMembershipRequest = z.infer<typeof cancelMembershipRequestSchema>;
