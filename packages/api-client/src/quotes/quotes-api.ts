import {
  mediaUploadUrlRequestSchema,
  mediaUploadUrlResponseSchema,
  quoteRequestResponseSchema,
  quoteRequestsResponseSchema,
  submitQuoteOfferRequestSchema,
  type QuoteRequestResponse,
  type SubmitQuoteOfferRequest,
  type MediaUploadUrlResponse,
  type MediaUploadUrlRequest,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class QuotesApi {
  constructor(private readonly client: ApiClientContext) {}

  async createQuotePhotoUploadUrl(
    request: MediaUploadUrlRequest,
  ): Promise<MediaUploadUrlResponse> {
    const body = mediaUploadUrlRequestSchema.parse({
      ...request,
      purpose: "quote_photo",
    });
    const response = await this.client.request("/api/v1/customer/media/upload-url", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return mediaUploadUrlResponseSchema.parse(await response.json());
  }

  async listCustomerQuotes(): Promise<QuoteRequestResponse[]> {
    const response = await this.client.request("/api/v1/quotes");
    const payload = quoteRequestsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async acceptQuote(quoteRequestId: string): Promise<QuoteRequestResponse> {
    const response = await this.client.request(
      `/api/v1/quotes/${encodeURIComponent(quoteRequestId)}/accept`,
      { method: "POST" },
    );
    return quoteRequestResponseSchema.parse(await response.json());
  }

  async declineQuote(quoteRequestId: string): Promise<QuoteRequestResponse> {
    const response = await this.client.request(
      `/api/v1/quotes/${encodeURIComponent(quoteRequestId)}/decline`,
      { method: "POST" },
    );
    return quoteRequestResponseSchema.parse(await response.json());
  }

  async listTenantQuotes(options: { locationId?: string } = {}): Promise<QuoteRequestResponse[]> {
    const query = new URLSearchParams();
    if (options.locationId) {
      query.set("locationId", options.locationId);
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/tenant/quotes${suffix}`);
    const payload = quoteRequestsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async submitQuoteOffer(
    quoteRequestId: string,
    request: SubmitQuoteOfferRequest,
  ): Promise<QuoteRequestResponse> {
    const body = submitQuoteOfferRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/quotes/${encodeURIComponent(quoteRequestId)}/offer`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return quoteRequestResponseSchema.parse(await response.json());
  }
}
