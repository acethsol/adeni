import {
  customerPackageLedgerSchema,
  customerPackageListSchema,
  customerPackageSchema,
  packageDefinitionListSchema,
  packageDefinitionSchema,
  packagePurchaseResponseSchema,
  publicPackageCatalogSchema,
  redeemableBenefitListSchema,
  type CreatePackageDefinitionRequest,
  type SellPackageRequest,
  type UpdatePackageDefinitionRequest,
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
    method: "POST" | "PATCH" = "POST",
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

  /** Signed-in customer's credits at one business, filtered to what applies to the booking being built. */
  myRedeemableBenefits(slug: string, params: { locationId?: string; serviceIds?: string[] } = {}, signal?: AbortSignal) {
    const query = new URLSearchParams();
    if (params.locationId) query.set("businessLocationId", params.locationId);
    for (const id of params.serviceIds ?? []) query.append("serviceOfferingIds", id);
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    return this.get(`${publicBase(slug)}/redeemable-benefits${suffix}`, redeemableBenefitListSchema, signal);
  }
}
