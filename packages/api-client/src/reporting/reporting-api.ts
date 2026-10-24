import {
  tenantDashboardSummarySchema,
  tenantDashboardSeriesSchema,
  tenantDashboardBreakdownSchema,
  tenantDashboardLayoutSchema,
  type TenantDashboardSummary,
  type TenantDashboardSeries,
  type TenantDashboardBreakdown,
  type TenantDashboardLayout,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class ReportingApi {
  constructor(private readonly client: ApiClientContext) {}

  async downloadTenantQuotePdf(quoteId: string): Promise<Blob> {
    const response = await this.client.request(
      `/api/v1/tenant/quotes/${encodeURIComponent(quoteId)}/pdf`,
    );
    return response.blob();
  }

  async downloadTenantCustomerStatementPdf(options: {
    tenantCustomerId: string;
    from: string;
    to: string;
  }): Promise<Blob> {
    const query = new URLSearchParams({
      from: options.from,
      to: options.to,
    });
    const response = await this.client.request(
      `/api/v1/tenant/customers/${encodeURIComponent(options.tenantCustomerId)}/statement?${query.toString()}`,
    );
    return response.blob();
  }

  async downloadCompensationExport(options: {
    tenantId: string;
    from: string;
    to: string;
    format?: "csv" | "json";
  }): Promise<Blob> {
    const query = new URLSearchParams({
      tenantId: options.tenantId,
      from: options.from,
      to: options.to,
      format: options.format ?? "csv",
    });
    const response = await this.client.request(`/api/v1/finance/compensation-export?${query.toString()}`);
    return response.blob();
  }

  async downloadTenantReportExport(options: {
    reportKey: string;
    from: string;
    to: string;
    format?: "csv" | "xlsx" | "pdf";
    locationId?: string;
  }): Promise<Blob> {
    const query = new URLSearchParams({
      from: options.from,
      to: options.to,
      format: options.format ?? "csv",
    });
    if (options.locationId) {
      query.set("locationId", options.locationId);
    }
    const response = await this.client.request(
      `/api/v1/tenant/reporting/exports/${encodeURIComponent(options.reportKey)}?${query.toString()}`,
    );
    return response.blob();
  }

  async getTenantDashboardSummary(
    from: string,
    to: string,
    locationId?: string,
  ): Promise<TenantDashboardSummary> {
    const query = new URLSearchParams({ from, to });
    if (locationId) {
      query.set("locationId", locationId);
    }
    const response = await this.client.request(`/api/v1/tenant/dashboard/summary?${query.toString()}`);
    return tenantDashboardSummarySchema.parse(await response.json());
  }

  async getTenantDashboardSeries(
    metric: "appointments" | "collected" | "booked_value",
    from: string,
    to: string,
    locationId?: string,
  ): Promise<TenantDashboardSeries> {
    const query = new URLSearchParams({ metric, from, to });
    if (locationId) {
      query.set("locationId", locationId);
    }
    const response = await this.client.request(`/api/v1/tenant/dashboard/series?${query.toString()}`);
    return tenantDashboardSeriesSchema.parse(await response.json());
  }

  async getTenantDashboardBreakdown(
    dimension: "status" | "service" | "staff",
    from: string,
    to: string,
    locationId?: string,
  ): Promise<TenantDashboardBreakdown> {
    const query = new URLSearchParams({ dimension, from, to });
    if (locationId) {
      query.set("locationId", locationId);
    }
    const response = await this.client.request(`/api/v1/tenant/dashboard/breakdown?${query.toString()}`);
    return tenantDashboardBreakdownSchema.parse(await response.json());
  }

  async getTenantDashboardLayout(): Promise<TenantDashboardLayout> {
    const response = await this.client.request("/api/v1/tenant/dashboard/layout");
    return tenantDashboardLayoutSchema.parse(await response.json());
  }

  async putTenantDashboardLayout(layout: TenantDashboardLayout): Promise<TenantDashboardLayout> {
    const payload = tenantDashboardLayoutSchema.parse(layout);
    const response = await this.client.request("/api/v1/tenant/dashboard/layout", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return tenantDashboardLayoutSchema.parse(await response.json());
  }
}
