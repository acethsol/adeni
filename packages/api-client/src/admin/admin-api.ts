import {
  adminCustomersResponseSchema,
  adminMarketsResponseSchema,
  adminMarketSchema,
  adminBusinessesResponseSchema,
  adminBusinessDirectoryStatsSchema,
  adminMarketCatalogStatsSchema,
  type AdminBusinessDirectoryStats,
  type AdminMarketCatalogStats,
  type AdminPagedResult,
  setTenantCommercialTermsRequestSchema,
  adminTenantCommercialTermsSchema,
  pricingCatalogVersionDetailSchema,
  pricingCatalogVersionListResponseSchema,
  tenantBillingHistoryResponseSchema,
  adminSubscriptionMetricsSchema,
  adminPlatformSubscriptionMrrSchema,
  adminGmvSummarySchema,
  type AdminPlatformSubscriptionMrr,
  type AdminGmvSummary,
  adminBusinessSummarySchema,
  adminRevenueMetricsSchema,
  adminTransactionsSearchResponseSchema,
  adminAuditLogsListResponseSchema,
  createMarketRequestSchema,
  customerDataExportSchema,
  pendingBusinessesResponseSchema,
  rejectBusinessRequestSchema,
  setMarketLiveRequestSchema,
  pendingBadgesResponseSchema,
  adminReviewsListResultSchema,
  type PendingBadgeRequest,
  type AdminReviewsListResult,
  updateMarketRequestSchema,
  type AdminCustomerSummary,
  type AdminMarket,
  type AdminBusinessSummary,
  type AdminTenantCommercialTerms,
  type SetTenantCommercialTermsRequest,
  type PricingCatalogVersionSummary,
  type PricingCatalogVersionDetail,
  type CreatePricingCatalogVersionRequest,
  type UpsertPricingCatalogEntriesRequest,
  type TenantSubscriptionCharge,
  type AdminSubscriptionMetrics,
  type AdminRevenueMetrics,
  type AdminTransactionSearchItem,
  type AdminAuditLogListItem,
  type CustomerDataExport,
  type PendingBusiness,
  appendListSortParams,
  type ListSortParams,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class AdminApi {
  constructor(private readonly client: ApiClientContext) {}

  async listAdminReviews(options: {
    q?: string;
    page?: number;
    pageSize?: number;
    includeHidden?: boolean;
  } = {}): Promise<AdminReviewsListResult> {
    const query = new URLSearchParams();
    if (options.q?.trim()) {
      query.set("q", options.q.trim());
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    if (options.includeHidden) {
      query.set("includeHidden", "true");
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/admin/reviews${suffix}`);
    return adminReviewsListResultSchema.parse(await response.json());
  }

  async hideAdminReview(reviewId: string): Promise<void> {
    await this.client.request(`/api/v1/admin/reviews/${encodeURIComponent(reviewId)}`, {
      method: "DELETE",
    });
  }

  async listAdminPendingBadges(
    options: { q?: string; page?: number; pageSize?: number } = {},
  ): Promise<AdminPagedResult<PendingBadgeRequest>> {
    const query = new URLSearchParams();
    if (options.q?.trim()) {
      query.set("q", options.q.trim());
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/admin/verification/pending-badges${suffix}`);
    return pendingBadgesResponseSchema.parse(await response.json());
  }

  async grantAdminVerificationBadge(tenantId: string, badgeType: string): Promise<void> {
    await this.client.request(
      `/api/v1/admin/verification/businesses/${encodeURIComponent(tenantId)}/badges/${encodeURIComponent(badgeType)}/grant`,
      { method: "POST" },
    );
  }

  async denyAdminVerificationBadge(tenantId: string, badgeType: string): Promise<void> {
    await this.client.request(
      `/api/v1/admin/verification/businesses/${encodeURIComponent(tenantId)}/badges/${encodeURIComponent(badgeType)}/revoke`,
      { method: "POST" },
    );
  }

  async downloadAdminReportExport(options: {
    reportKey: string;
    from: string;
    to: string;
    format?: "csv" | "xlsx";
    q?: string;
    kind?: string;
    catalogVersionCode?: string;
  }): Promise<Blob> {
    const query = new URLSearchParams({
      from: options.from,
      to: options.to,
      format: options.format ?? "csv",
    });
    if (options.q?.trim()) {
      query.set("q", options.q.trim());
    }
    if (options.kind?.trim()) {
      query.set("kind", options.kind.trim());
    }
    if (options.catalogVersionCode?.trim()) {
      query.set("catalogVersionCode", options.catalogVersionCode.trim());
    }
    const response = await this.client.request(
      `/api/v1/admin/reporting/exports/${encodeURIComponent(options.reportKey)}?${query.toString()}`,
    );
    return response.blob();
  }

  async listPendingBusinesses(
    options: { q?: string; page?: number; pageSize?: number; sort?: ListSortParams } = {},
  ): Promise<AdminPagedResult<PendingBusiness>> {
    const query = new URLSearchParams();
    if (options.q?.trim()) {
      query.set("q", options.q.trim());
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    appendListSortParams(query, options.sort);
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/admin/businesses/pending${suffix}`);
    return pendingBusinessesResponseSchema.parse(await response.json());
  }

  async approvePendingBusiness(tenantId: string): Promise<void> {
    await this.client.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/approve`,
      { method: "POST" },
    );
  }

  async rejectPendingBusiness(tenantId: string, reason: string): Promise<void> {
    const body = rejectBusinessRequestSchema.parse({ reason });
    await this.client.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/reject`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
  }

  async listAdminBusinesses(
    options: { q?: string; page?: number; pageSize?: number; sort?: ListSortParams } = {},
  ): Promise<AdminPagedResult<AdminBusinessSummary>> {
    const query = new URLSearchParams();
    if (options.q?.trim()) {
      query.set("q", options.q.trim());
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    appendListSortParams(query, options.sort);
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/admin/businesses${suffix}`);
    return adminBusinessesResponseSchema.parse(await response.json());
  }

  async getAdminBusinessDirectoryStats(): Promise<AdminBusinessDirectoryStats> {
    const response = await this.client.request("/api/v1/admin/businesses/stats");
    return adminBusinessDirectoryStatsSchema.parse(await response.json());
  }

  async getAdminSubscriptionMetrics(): Promise<AdminSubscriptionMetrics> {
    const response = await this.client.request("/api/v1/admin/metrics/subscription");
    return adminSubscriptionMetricsSchema.parse(await response.json());
  }

  async getAdminPlatformSubscriptionMrr(): Promise<AdminPlatformSubscriptionMrr> {
    const response = await this.client.request("/api/v1/admin/reporting/platform-subscription-mrr");
    return adminPlatformSubscriptionMrrSchema.parse(await response.json());
  }

  async getAdminReportingGmv(
    granularity: "month" | "year" = "month",
  ): Promise<AdminGmvSummary> {
    const query = new URLSearchParams({ granularity });
    const response = await this.client.request(
      `/api/v1/admin/reporting/gmv?${query.toString()}`,
    );
    return adminGmvSummarySchema.parse(await response.json());
  }

  async getAdminRevenueMetrics(
    granularity: "month" | "year" = "month",
  ): Promise<AdminRevenueMetrics> {
    const query = new URLSearchParams({ granularity });
    const response = await this.client.request(
      `/api/v1/admin/metrics/revenue?${query.toString()}`,
    );
    return adminRevenueMetricsSchema.parse(await response.json());
  }

  async searchAdminTransactions(
    queryText: string | null | undefined,
    options: {
      kind?: "all" | "commerce" | "saas";
      page?: number;
      pageSize?: number;
      sort?: ListSortParams;
    } = {},
  ): Promise<AdminPagedResult<AdminTransactionSearchItem>> {
    const query = new URLSearchParams();
    const trimmed = queryText?.trim();
    if (trimmed) {
      query.set("q", trimmed);
    }
    query.set("kind", options.kind ?? "all");
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    appendListSortParams(query, options.sort);
    const response = await this.client.request(`/api/v1/admin/transactions?${query.toString()}`);
    return adminTransactionsSearchResponseSchema.parse(await response.json());
  }

  async listAdminAuditLogs(
    options: {
      action?: string;
      from?: string;
      to?: string;
      page?: number;
      pageSize?: number;
    } = {},
  ): Promise<AdminPagedResult<AdminAuditLogListItem>> {
    const query = new URLSearchParams();
    if (options.action?.trim()) {
      query.set("action", options.action.trim());
    }
    if (options.from?.trim()) {
      query.set("from", options.from.trim());
    }
    if (options.to?.trim()) {
      query.set("to", options.to.trim());
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/admin/audit-logs${suffix}`);
    return adminAuditLogsListResponseSchema.parse(await response.json());
  }

  async getAdminBusiness(tenantId: string): Promise<AdminBusinessSummary> {
    const response = await this.client.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}`,
    );
    return adminBusinessSummarySchema.parse(await response.json());
  }

  async listAdminTenantCommercialAudit(
    tenantId: string,
    limit = 30,
  ): Promise<
    Array<{
      id: string;
      actorId: string;
      action: string;
      occurredAt: string;
      metadataJson?: string | null;
    }>
  > {
    const response = await this.client.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/commercial-audit?limit=${limit}`,
    );
    const body = (await response.json()) as { items: unknown[] };
    return body.items as Array<{
      id: string;
      actorId: string;
      action: string;
      occurredAt: string;
      metadataJson?: string | null;
    }>;
  }

  async getAdminTenantCommercialTerms(tenantId: string): Promise<AdminTenantCommercialTerms> {
    const response = await this.client.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/commercial-terms`,
    );
    return adminTenantCommercialTermsSchema.parse(await response.json());
  }

  async setAdminTenantCommercialTerms(
    tenantId: string,
    body: SetTenantCommercialTermsRequest,
  ): Promise<void> {
    const payload = setTenantCommercialTermsRequestSchema.parse(body);
    await this.client.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/commercial-terms`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );
  }

  async listAdminPricingCatalogVersions(): Promise<PricingCatalogVersionSummary[]> {
    const response = await this.client.request("/api/v1/admin/pricing-catalog/versions");
    return pricingCatalogVersionListResponseSchema.parse(await response.json()).items;
  }

  async getAdminPricingCatalogVersion(code: string): Promise<PricingCatalogVersionDetail> {
    const response = await this.client.request(
      `/api/v1/admin/pricing-catalog/versions/${encodeURIComponent(code)}`,
    );
    return pricingCatalogVersionDetailSchema.parse(await response.json());
  }

  async createAdminPricingCatalogVersion(
    payload: CreatePricingCatalogVersionRequest,
  ): Promise<PricingCatalogVersionDetail> {
    const response = await this.client.request("/api/v1/admin/pricing-catalog/versions", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return pricingCatalogVersionDetailSchema.parse(await response.json());
  }

  async upsertAdminPricingCatalogEntries(
    code: string,
    payload: UpsertPricingCatalogEntriesRequest,
  ): Promise<PricingCatalogVersionDetail> {
    const response = await this.client.request(
      `/api/v1/admin/pricing-catalog/versions/${encodeURIComponent(code)}/entries`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );
    return pricingCatalogVersionDetailSchema.parse(await response.json());
  }

  async activateAdminPricingCatalogVersion(code: string): Promise<PricingCatalogVersionDetail> {
    const response = await this.client.request(
      `/api/v1/admin/pricing-catalog/versions/${encodeURIComponent(code)}/activate`,
      { method: "POST" },
    );
    return pricingCatalogVersionDetailSchema.parse(await response.json());
  }

  async listAdminTenantSubscriptionCharges(
    tenantId: string,
    limit = 20,
  ): Promise<TenantSubscriptionCharge[]> {
    const query = new URLSearchParams();
    if (limit > 0) {
      query.set("limit", String(limit));
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(
      `/api/v1/admin/businesses/${encodeURIComponent(tenantId)}/subscription-charges${suffix}`,
    );
    return tenantBillingHistoryResponseSchema.parse(await response.json()).items;
  }

  async searchAdminCustomers(
    queryText: string,
    options: { page?: number; pageSize?: number } = {},
  ): Promise<AdminPagedResult<AdminCustomerSummary>> {
    const query = new URLSearchParams({ q: queryText.trim() });
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    const response = await this.client.request(`/api/v1/admin/customers?${query.toString()}`);
    return adminCustomersResponseSchema.parse(await response.json());
  }

  async exportAdminCustomer(customerId: string): Promise<CustomerDataExport> {
    const response = await this.client.request(
      `/api/v1/admin/customers/${encodeURIComponent(customerId)}/export`,
    );
    return customerDataExportSchema.parse(await response.json());
  }

  async initiateAdminCustomerDelete(customerId: string): Promise<void> {
    await this.client.request(
      `/api/v1/admin/customers/${encodeURIComponent(customerId)}/delete`,
      { method: "POST" },
    );
  }

  async getAdminMarketCatalogStats(): Promise<AdminMarketCatalogStats> {
    const response = await this.client.request("/api/v1/admin/markets/stats");
    return adminMarketCatalogStatsSchema.parse(await response.json());
  }

  async listAdminMarkets(
    options: { q?: string; page?: number; pageSize?: number; sort?: ListSortParams } = {},
  ): Promise<AdminPagedResult<AdminMarket>> {
    const query = new URLSearchParams();
    if (options.q?.trim()) {
      query.set("q", options.q.trim());
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    appendListSortParams(query, options.sort);
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/admin/markets${suffix}`);
    return adminMarketsResponseSchema.parse(await response.json());
  }

  async createAdminMarket(
    body: Parameters<typeof createMarketRequestSchema.parse>[0],
  ): Promise<AdminMarket> {
    const payload = createMarketRequestSchema.parse(body);
    const response = await this.client.request("/api/v1/admin/markets", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return adminMarketSchema.parse(await response.json());
  }

  async updateAdminMarket(
    id: string,
    body: Parameters<typeof updateMarketRequestSchema.parse>[0],
  ): Promise<AdminMarket> {
    const payload = updateMarketRequestSchema.parse(body);
    const response = await this.client.request(`/api/v1/admin/markets/${encodeURIComponent(id)}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return adminMarketSchema.parse(await response.json());
  }

  async setAdminMarketLive(id: string, isLive: boolean): Promise<void> {
    const body = setMarketLiveRequestSchema.parse({ isLive });
    await this.client.request(`/api/v1/admin/markets/${encodeURIComponent(id)}/live`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
  }
}
