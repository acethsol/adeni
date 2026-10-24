import {
  type PagedResult,
  tenantBillingHistoryResponseSchema,
  billingDocumentResponseSchema,
  billingDocumentsResponseSchema,
  createInvoiceRequestSchema,
  paymentReceiptResponseSchema,
  initializePaymentRequestSchema,
  paymentIntentResponseSchema,
  createPaymentLinkRequestSchema,
  paymentLedgerResponseSchema,
  refundPaymentRequestSchema,
  type TenantSubscriptionCharge,
  type BillingDocumentResponse,
  type CreateInvoiceRequest,
  type PaymentReceiptResponse,
  type InitializePaymentRequest,
  type PaymentIntentResponse,
  type CreatePaymentLinkRequest,
  type PaymentLedgerEntry,
  type RefundPaymentRequest,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class FinanceApi {
  constructor(private readonly client: ApiClientContext) {}

  async getPaymentReceipt(paymentIntentId: string): Promise<PaymentReceiptResponse> {
    const response = await this.client.request(
      `/api/v1/finance/${encodeURIComponent(paymentIntentId)}/receipt`,
    );
    return paymentReceiptResponseSchema.parse(await response.json());
  }

  async listBillingDocuments(options?: {
    kind?: string;
    bookingId?: string;
    page?: number;
    pageSize?: number;
  }): Promise<BillingDocumentResponse[]> {
    const params = new URLSearchParams();
    if (options?.kind) params.set("kind", options.kind);
    if (options?.bookingId) params.set("bookingId", options.bookingId);
    if (options?.page) params.set("page", String(options.page));
    if (options?.pageSize) params.set("pageSize", String(options.pageSize));
    const query = params.toString();
    const response = await this.client.request(
      `/api/v1/finance/documents${query ? `?${query}` : ""}`,
    );
    return billingDocumentsResponseSchema.parse(await response.json()).items;
  }

  async getBillingDocument(documentId: string): Promise<BillingDocumentResponse> {
    const response = await this.client.request(
      `/api/v1/finance/documents/${encodeURIComponent(documentId)}`,
    );
    return billingDocumentResponseSchema.parse(await response.json());
  }

  async getBillingDocumentPdfUrl(documentId: string): Promise<string> {
    return `${this.client.baseUrl}/api/v1/finance/documents/${encodeURIComponent(documentId)}/pdf`;
  }

  async downloadBillingDocumentPdf(documentId: string): Promise<Blob> {
    const response = await this.client.request(
      `/api/v1/finance/documents/${encodeURIComponent(documentId)}/pdf`,
    );
    return response.blob();
  }

  async emailBillingDocument(
    documentId: string,
    toEmail?: string,
  ): Promise<BillingDocumentResponse> {
    const response = await this.client.request(
      `/api/v1/finance/documents/${encodeURIComponent(documentId)}/email`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ toEmail: toEmail ?? null }),
      },
    );
    return billingDocumentResponseSchema.parse(await response.json());
  }

  async createInvoice(request: CreateInvoiceRequest): Promise<BillingDocumentResponse> {
    const body = createInvoiceRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/finance/documents/invoices", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return billingDocumentResponseSchema.parse(await response.json());
  }

  async getPublicBillingDocument(token: string): Promise<BillingDocumentResponse> {
    const response = await this.client.request(
      `/api/v1/public/documents/${encodeURIComponent(token)}`,
    );
    return billingDocumentResponseSchema.parse(await response.json());
  }

  async initializePayment(
    request: InitializePaymentRequest,
    idempotencyKey?: string,
  ): Promise<PaymentIntentResponse> {
    const body = initializePaymentRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/finance/initialize", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      idempotencyKey,
    });
    return paymentIntentResponseSchema.parse(await response.json());
  }

  async getPayment(id: string): Promise<PaymentIntentResponse> {
    const response = await this.client.request(`/api/v1/finance/${id}`);
    return paymentIntentResponseSchema.parse(await response.json());
  }

  async createPaymentLink(
    request: CreatePaymentLinkRequest,
  ): Promise<PaymentIntentResponse> {
    const body = createPaymentLinkRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/finance/links", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return paymentIntentResponseSchema.parse(await response.json());
  }

  async listPaymentLedger(
    tenantId: string,
    page = 1,
    pageSize = 50,
    locationId?: string,
  ): Promise<PagedResult<PaymentLedgerEntry>> {
    const query = new URLSearchParams({
      tenantId,
      page: String(page),
      pageSize: String(pageSize),
    });
    if (locationId) {
      query.set("locationId", locationId);
    }
    const response = await this.client.request(`/api/v1/finance/ledger?${query.toString()}`);
    return paymentLedgerResponseSchema.parse(await response.json());
  }

  async refundPayment(
    paymentIntentId: string,
    request: RefundPaymentRequest,
  ): Promise<PaymentIntentResponse> {
    const body = refundPaymentRequestSchema.parse(request);
    const response = await this.client.request(`/api/v1/finance/${paymentIntentId}/refund`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return paymentIntentResponseSchema.parse(await response.json());
  }

  async confirmStubPayment(reference: string): Promise<PaymentIntentResponse> {
    const response = await this.client.request("/api/v1/finance/stub/confirm", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ reference }),
    });
    return paymentIntentResponseSchema.parse(await response.json());
  }

  async getTenantBillingHistory(limit = 20): Promise<TenantSubscriptionCharge[]> {
    const response = await this.client.request(
      `/api/v1/tenant/billing?limit=${encodeURIComponent(String(limit))}`,
    );
    return tenantBillingHistoryResponseSchema.parse(await response.json()).items;
  }
}
