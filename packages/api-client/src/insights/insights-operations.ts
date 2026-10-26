import { customerSegmentsSchema, customerSegmentsBatchRequestSchema, customerSegmentDefinitionSchema } from "@adeni/shared/customer-segments";
import type { ApiClientContext } from "../core/api-transport";
export class InsightsOperations {
  constructor(private readonly client: ApiClientContext) {}
  async segments(id: string, locationId?: string, signal?: AbortSignal) {
    const response = await this.client.request(`/api/v1/tenant/customers/${encodeURIComponent(id)}/segments${this.scope(locationId)}`, { signal, cache: "no-store" });
    return customerSegmentsSchema.parse(await response.json());
  }
  async batch(customerIds: string[], locationId?: string, signal?: AbortSignal) {
    const body = customerSegmentsBatchRequestSchema.parse({ customerIds, locationId });
    const response = await this.client.request("/api/v1/tenant/customers/segments/batch", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body), signal, cache: "no-store" });
    return customerSegmentsSchema.array().parse(await response.json());
  }
  async definitions(signal?: AbortSignal) {
    const response = await this.client.request("/api/v1/tenant/customer-segments/definitions", { signal });
    return customerSegmentDefinitionSchema.array().parse(await response.json());
  }
  async preview(id: string, locationId?: string, signal?: AbortSignal): Promise<Blob> {
    const response = await this.client.request(`/api/v1/tenant/reporting/documents/${encodeURIComponent(id)}/preview${this.scope(locationId)}`, { signal, cache: "no-store" });
    if (response.headers.get("Content-Type")?.split(";")[0]?.trim().toLowerCase() !== "application/pdf") throw new Error("Unexpected private document response.");
    const blob = await response.blob();
    if (blob.size > 5 * 1024 * 1024) throw new Error("Private document exceeds viewer limit.");
    return blob;
  }
  private scope(id?: string): string { return id ? `?locationId=${encodeURIComponent(id)}` : ""; }
}
