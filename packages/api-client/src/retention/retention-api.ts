import {
  customerPackageLedgerSchema,
  customerPackageListSchema,
  customerMembershipListSchema,
  customerMembershipSchema,
  customerPackageSchema,
  membershipPlanListSchema,
  membershipPlanSchema,
  membershipSubscribeResponseSchema,
  packageDefinitionListSchema,
  packageDefinitionSchema,
  packagePurchaseResponseSchema,
  publicMembershipPlanCatalogSchema,
  publicPackageCatalogSchema,
  redeemableBenefitListSchema,
  tenantMembershipMrrResponseSchema,
  loyaltyProgramSchema,
  loyaltyAccountSchema,
  loyaltyTransactionListSchema,
  loyaltyRedeemQuoteSchema,
  customerLoyaltyAtBusinessSchema,
  customerLoyaltyListSchema,
  loyaltyTierListSchema,
  loyaltyRewardListSchema,
  loyaltyRewardSchema,
  loyaltyRewardRedemptionSchema,
  referralProgramSchema,
  referralCodeSchema,
  type CreateMembershipPlanRequest,
  type UpsertReferralProgramRequest,
  type CreateLoyaltyRewardRequest,
  type ReplaceLoyaltyTiersRequest,
  type UpdateLoyaltyRewardRequest,
  type CreatePackageDefinitionRequest,
  type EnrolMembershipRequest,
  type RenewMembershipRequest,
  type SellPackageRequest,
  type UpdateMembershipPlanRequest,
  type UpdatePackageDefinitionRequest,
  type UpsertLoyaltyProgramRequest,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

const BASE = "/api/v1/tenant/retention";
const publicBase = (slug: string) => `/api/v1/businesses/${encodeURIComponent(slug)}/retention`;

/** Tenant retention API: package catalog, in-person sales, customer packages and the benefit ledger. */
export class RetentionApi {
  constructor(private readonly client: ApiClientContext) {}

  private async get<T>(path: string, schema: { parse(value: unknown): T }, signal?: AbortSignal): Promise<T> {
    return schema.parse(await (await this.client.request(path, { signal, cache: "no-store" })).json());
  }

  private async write<T>(
    path: string,
    body: unknown,
    schema: { parse(value: unknown): T },
    method: "POST" | "PATCH" | "PUT" = "POST",
    key?: string,
    signal?: AbortSignal,
  ): Promise<T> {
    const response = await this.client.request(path, {
      method,
      signal,
      cache: "no-store",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body ?? {}),
      idempotencyKey: key,
    });
    return schema.parse(await response.json());
  }

  listPackages(activeOnly = false, signal?: AbortSignal) {
    return this.get(`${BASE}/packages?activeOnly=${activeOnly}`, packageDefinitionListSchema, signal);
  }

  getPackage(id: string, signal?: AbortSignal) {
    return this.get(`${BASE}/packages/${encodeURIComponent(id)}`, packageDefinitionSchema, signal);
  }

  createPackage(body: CreatePackageDefinitionRequest, signal?: AbortSignal) {
    return this.write(`${BASE}/packages`, body, packageDefinitionSchema, "POST", undefined, signal);
  }

  updatePackage(id: string, body: UpdatePackageDefinitionRequest, signal?: AbortSignal) {
    return this.write(`${BASE}/packages/${encodeURIComponent(id)}`, body, packageDefinitionSchema, "PATCH", undefined, signal);
  }

  deactivatePackage(id: string, signal?: AbortSignal) {
    return this.write(`${BASE}/packages/${encodeURIComponent(id)}/deactivate`, {}, packageDefinitionSchema, "POST", undefined, signal);
  }

  /** Membership plan catalog (Sprint 37.2-A). Requires the `memberships` plan entitlement. */
  listMembershipPlans(activeOnly = false, signal?: AbortSignal) {
    return this.get(`${BASE}/memberships/plans?activeOnly=${activeOnly}`, membershipPlanListSchema, signal);
  }

  getMembershipPlan(id: string, signal?: AbortSignal) {
    return this.get(`${BASE}/memberships/plans/${encodeURIComponent(id)}`, membershipPlanSchema, signal);
  }

  createMembershipPlan(body: CreateMembershipPlanRequest, signal?: AbortSignal) {
    return this.write(`${BASE}/memberships/plans`, body, membershipPlanSchema, "POST", undefined, signal);
  }

  updateMembershipPlan(id: string, body: UpdateMembershipPlanRequest, signal?: AbortSignal) {
    return this.write(`${BASE}/memberships/plans/${encodeURIComponent(id)}`, body, membershipPlanSchema, "PATCH", undefined, signal);
  }

  deactivateMembershipPlan(id: string, signal?: AbortSignal) {
    return this.write(`${BASE}/memberships/plans/${encodeURIComponent(id)}/deactivate`, {}, membershipPlanSchema, "POST", undefined, signal);
  }

  /** Customer memberships (Sprint 37.2-B). Requires the `memberships` plan entitlement. */
  listCustomerMemberships(tenantCustomerId: string, signal?: AbortSignal) {
    return this.get(`${BASE}/customers/${encodeURIComponent(tenantCustomerId)}/memberships`, customerMembershipListSchema, signal);
  }

  getCustomerMembership(id: string, signal?: AbortSignal) {
    return this.get(`${BASE}/customer-memberships/${encodeURIComponent(id)}`, customerMembershipSchema, signal);
  }

  /** In-person enrolment. `key` is the required Idempotency-Key — reuse it when retrying the same enrolment. */
  enrolMembership(tenantCustomerId: string, body: EnrolMembershipRequest, key: string, signal?: AbortSignal) {
    return this.write(
      `${BASE}/customers/${encodeURIComponent(tenantCustomerId)}/memberships/enrol`,
      body,
      customerMembershipSchema,
      "POST",
      key,
      signal,
    );
  }

  /** Owner-only; Active → Frozen when the plan allows it. */
  freezeCustomerMembership(id: string, signal?: AbortSignal) {
    return this.write(`${BASE}/customer-memberships/${encodeURIComponent(id)}/freeze`, {}, customerMembershipSchema, "POST", undefined, signal);
  }

  /** Owner-only; Frozen → Active. */
  unfreezeCustomerMembership(id: string, signal?: AbortSignal) {
    return this.write(`${BASE}/customer-memberships/${encodeURIComponent(id)}/unfreeze`, {}, customerMembershipSchema, "POST", undefined, signal);
  }

  /** Owner-only; applies the plan's cancellation policy (immediate or end of cycle). */
  cancelCustomerMembership(id: string, reason: string | undefined, signal?: AbortSignal) {
    return this.write(`${BASE}/customer-memberships/${encodeURIComponent(id)}/cancel`, { reason }, customerMembershipSchema, "POST", undefined, signal);
  }

  /** In-person renewal once the paid cycle has ended. Idempotency-Key required. */
  renewCustomerMembership(id: string, body: RenewMembershipRequest, key: string, signal?: AbortSignal) {
    return this.write(`${BASE}/customer-memberships/${encodeURIComponent(id)}/renew`, body, customerMembershipSchema, "POST", key, signal);
  }

  /** TenantMembershipMrr per currency (not Adeni SaaS MRR). */
  membershipMrr(signal?: AbortSignal) {
    return this.get(`${BASE}/memberships/mrr`, tenantMembershipMrrResponseSchema, signal);
  }

  listCustomerPackages(tenantCustomerId: string, signal?: AbortSignal) {
    return this.get(`${BASE}/customers/${encodeURIComponent(tenantCustomerId)}/packages`, customerPackageListSchema, signal);
  }

  /** In-person sale. `key` is the required Idempotency-Key — reuse it when retrying the same sale. */
  sellPackage(tenantCustomerId: string, body: SellPackageRequest, key: string, signal?: AbortSignal) {
    return this.write(`${BASE}/customers/${encodeURIComponent(tenantCustomerId)}/packages/sell`, body, customerPackageSchema, "POST", key, signal);
  }

  listRedeemableBenefits(tenantCustomerId: string, params: { locationId?: string; serviceIds?: string[] } = {}, signal?: AbortSignal) {
    const query = new URLSearchParams();
    if (params.locationId) query.set("businessLocationId", params.locationId);
    for (const id of params.serviceIds ?? []) query.append("serviceOfferingIds", id);
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    return this.get(`${BASE}/customers/${encodeURIComponent(tenantCustomerId)}/redeemable-benefits${suffix}`, redeemableBenefitListSchema, signal);
  }

  getCustomerPackage(id: string, signal?: AbortSignal) {
    return this.get(`${BASE}/customer-packages/${encodeURIComponent(id)}`, customerPackageSchema, signal);
  }

  getLedger(id: string, signal?: AbortSignal) {
    return this.get(`${BASE}/customer-packages/${encodeURIComponent(id)}/ledger`, customerPackageLedgerSchema, signal);
  }

  /** Owner-only; audited. Releases reservations and forfeits the remaining credits. */
  voidCustomerPackage(id: string, reason: string | undefined, key: string, signal?: AbortSignal) {
    return this.write(`${BASE}/customer-packages/${encodeURIComponent(id)}/void`, { reason }, customerPackageSchema, "POST", key, signal);
  }

  /** Signed-in consumer's packages across businesses. */
  myPackages(signal?: AbortSignal) {
    return this.get("/api/v1/me/retention/packages", customerPackageListSchema, signal);
  }

  /** Signed-in consumer's memberships across businesses (excludes unpaid pending checkouts). */
  myMemberships(signal?: AbortSignal) {
    return this.get("/api/v1/me/retention/memberships", customerMembershipListSchema, signal);
  }

  freezeMyMembership(id: string, signal?: AbortSignal) {
    return this.write(`/api/v1/me/retention/memberships/${encodeURIComponent(id)}/freeze`, {}, customerMembershipSchema, "POST", undefined, signal);
  }

  unfreezeMyMembership(id: string, signal?: AbortSignal) {
    return this.write(`/api/v1/me/retention/memberships/${encodeURIComponent(id)}/unfreeze`, {}, customerMembershipSchema, "POST", undefined, signal);
  }

  cancelMyMembership(id: string, reason: string | undefined, signal?: AbortSignal) {
    return this.write(`/api/v1/me/retention/memberships/${encodeURIComponent(id)}/cancel`, { reason }, customerMembershipSchema, "POST", undefined, signal);
  }

  /** Anonymous Discover storefront: sellable packages for a business (+ whether online purchase is live). */
  publicCatalog(slug: string, signal?: AbortSignal) {
    return this.get(`${publicBase(slug)}/packages`, publicPackageCatalogSchema, signal);
  }

  /** Customer online purchase. Credits are granted only after payment completes; reuse `key` on retries. */
  purchasePackage(slug: string, packageId: string, key: string, signal?: AbortSignal) {
    return this.write(
      `${publicBase(slug)}/packages/${encodeURIComponent(packageId)}/purchase`,
      {},
      packagePurchaseResponseSchema,
      "POST",
      key,
      signal,
    );
  }

  /** Anonymous Discover storefront: active membership plans (+ whether online subscribe is live). */
  publicMembershipPlans(slug: string, signal?: AbortSignal) {
    return this.get(`${publicBase(slug)}/memberships/plans`, publicMembershipPlanCatalogSchema, signal);
  }

  /** Customer online subscribe. The membership stays pending until PaymentCompleted. Reuse `key` on retries. */
  subscribeMembership(slug: string, planId: string, key: string, signal?: AbortSignal) {
    return this.write(
      `${publicBase(slug)}/memberships/plans/${encodeURIComponent(planId)}/subscribe`,
      {},
      membershipSubscribeResponseSchema,
      "POST",
      key,
      signal,
    );
  }

  /** Loyalty program settings (Sprint 37.3-A). Requires the `loyaltyBasic` entitlement; `configured:false` until saved. */
  getLoyaltyProgram(signal?: AbortSignal) {
    return this.get(`${BASE}/loyalty/program`, loyaltyProgramSchema, signal);
  }

  saveLoyaltyProgram(body: UpsertLoyaltyProgramRequest, signal?: AbortSignal) {
    return this.write(`${BASE}/loyalty/program`, body, loyaltyProgramSchema, "PUT", undefined, signal);
  }

  getLoyaltyCustomerAccount(tenantCustomerId: string, signal?: AbortSignal) {
    return this.get(`${BASE}/loyalty/customers/${tenantCustomerId}/account`, loyaltyAccountSchema, signal);
  }

  listLoyaltyCustomerTransactions(tenantCustomerId: string, limit = 50, signal?: AbortSignal) {
    const query = limit > 0 ? `?limit=${limit}` : "";
    return this.get(
      `${BASE}/loyalty/customers/${tenantCustomerId}/transactions${query}`,
      loyaltyTransactionListSchema,
      signal,
    );
  }

  /** Signed-in customer's credits at one business, filtered to what applies to the booking being built. */
  myLoyaltyAtBusiness(slug: string, signal?: AbortSignal) {
    return this.get(`${publicBase(slug)}/loyalty`, customerLoyaltyAtBusinessSchema, signal);
  }

  quoteLoyaltyRedeem(
    slug: string,
    params: { points: number; payableAfterCredits: number; currency: string },
    signal?: AbortSignal,
  ) {
    const query = new URLSearchParams({
      points: String(params.points),
      payableAfterCredits: String(params.payableAfterCredits),
      currency: params.currency,
    });
    return this.get(`${publicBase(slug)}/loyalty/quote?${query.toString()}`, loyaltyRedeemQuoteSchema, signal);
  }

  myLoyaltyBalances(signal?: AbortSignal) {
    return this.get("/api/v1/me/retention/loyalty", customerLoyaltyListSchema, signal);
  }

  /** Tier thresholds (Sprint 37.3-D). Requires `loyaltyTiers`; empty list when not entitled. */
  listLoyaltyTiers(signal?: AbortSignal) {
    return this.get(`${BASE}/loyalty/tiers`, loyaltyTierListSchema, signal);
  }

  saveLoyaltyTiers(body: ReplaceLoyaltyTiersRequest, signal?: AbortSignal) {
    return this.write(`${BASE}/loyalty/tiers`, body, loyaltyTierListSchema, "PUT", undefined, signal);
  }

  listLoyaltyRewards(activeOnly = true, signal?: AbortSignal) {
    return this.get(`${BASE}/loyalty/rewards?activeOnly=${activeOnly}`, loyaltyRewardListSchema, signal);
  }

  createLoyaltyReward(body: CreateLoyaltyRewardRequest, signal?: AbortSignal) {
    return this.write(`${BASE}/loyalty/rewards`, body, loyaltyRewardSchema, "POST", undefined, signal);
  }

  updateLoyaltyReward(id: string, body: UpdateLoyaltyRewardRequest, signal?: AbortSignal) {
    return this.write(`${BASE}/loyalty/rewards/${encodeURIComponent(id)}`, body, loyaltyRewardSchema, "PATCH", undefined, signal);
  }

  getReferralProgram(signal?: AbortSignal) {
    return this.get(`${BASE}/loyalty/referrals/program`, referralProgramSchema, signal);
  }

  saveReferralProgram(body: UpsertReferralProgramRequest, signal?: AbortSignal) {
    return this.write(`${BASE}/loyalty/referrals/program`, body, referralProgramSchema, "PUT", undefined, signal);
  }

  getCustomerReferralCode(tenantCustomerId: string, signal?: AbortSignal) {
    return this.get(`${BASE}/loyalty/customers/${tenantCustomerId}/referral-code`, referralCodeSchema, signal);
  }

  redeemLoyaltyRewardForCustomer(tenantCustomerId: string, rewardId: string, key: string, signal?: AbortSignal) {
    return this.write(
      `${BASE}/loyalty/customers/${encodeURIComponent(tenantCustomerId)}/rewards/${encodeURIComponent(rewardId)}/redeem`,
      {},
      loyaltyRewardRedemptionSchema,
      "POST",
      key,
      signal,
    );
  }

  myRedeemableBenefits(slug: string, params: { locationId?: string; serviceIds?: string[] } = {}, signal?: AbortSignal) {
    const query = new URLSearchParams();
    if (params.locationId) query.set("businessLocationId", params.locationId);
    for (const id of params.serviceIds ?? []) query.append("serviceOfferingIds", id);
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    return this.get(`${publicBase(slug)}/redeemable-benefits${suffix}`, redeemableBenefitListSchema, signal);
  }
}
