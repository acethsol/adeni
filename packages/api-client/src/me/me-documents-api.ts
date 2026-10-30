import { billingDocumentResponseSchema, billingDocumentsResponseSchema, type BillingDocumentResponse } from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class MeDocumentsApi {
  constructor(private readonly client: ApiClientContext) {}

  async listMyDocuments(options?: {
    kind?: "receipt" | "invoice";
    page?: number;
    pageSize?: number;
  }): Promise<BillingDocumentResponse[]> {
    const params = new URLSearchParams();
    if (options?.kind) {
      params.set("kind", options.kind);
    }
    if (options?.page) {
      params.set("page", String(options.page));
    }
    if (options?.pageSize) {
      params.set("pageSize", String(options.pageSize));
    }
    const query = params.toString();
    const response = await this.client.request(
      `/api/v1/me/documents${query ? `?${query}` : ""}`,
    );
    const payload = billingDocumentsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async getMyDocument(documentId: string): Promise<BillingDocumentResponse> {
    const response = await this.client.request(
      `/api/v1/me/documents/${encodeURIComponent(documentId)}`,
    );
    return billingDocumentResponseSchema.parse(await response.json());
  }
}
