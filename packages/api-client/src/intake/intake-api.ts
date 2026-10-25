import { intakeTemplateSchema, intakeViewsSchema, intakeStatusBatchSchema, intakeSummariesSchema, intakeStatusesSchema, intakeBindingSchema, intakeStatusSchema, intakeViewSchema, type IntakeDefinition, type IntakeAnswer } from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";
export class IntakeApi {
  constructor(private readonly client: ApiClientContext) {}
  private async get<T>(path: string, schema: { parse(value: unknown): T }, signal?: AbortSignal): Promise<T> {
    return schema.parse(await (await this.client.request(path, { signal, cache: "no-store" })).json());
  }
  private async write<T>(path: string, body: unknown, schema: { parse(value: unknown): T }, method = "POST", signal?: AbortSignal, key?: string): Promise<T> {
    const response = await this.client.request(path, { method, signal, cache: "no-store", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body), idempotencyKey: key });
    return schema.parse(await response.json());
  }
  listTemplates(page = 1, signal?: AbortSignal) { return this.get(`/api/v1/tenant/intake/templates?page=${page}`, intakeSummariesSchema, signal); }
  getTemplate(id: string, signal?: AbortSignal) { return this.get(`/api/v1/tenant/intake/templates/${encodeURIComponent(id)}`, intakeTemplateSchema, signal); }
  createTemplate(name: string) { return this.write("/api/v1/tenant/intake/templates", { name }, intakeTemplateSchema); }
  saveDraft(id: string, revision: string, definition: IntakeDefinition) { return this.write(`/api/v1/tenant/intake/templates/${encodeURIComponent(id)}/draft`, { revision, definition }, intakeTemplateSchema, "PUT"); }
  cloneDraft(id: string, revision: string) { return this.write(`/api/v1/tenant/intake/templates/${encodeURIComponent(id)}/draft`, { revision }, intakeTemplateSchema); }
  publish(id: string, revision: string) { return this.write(`/api/v1/tenant/intake/templates/${encodeURIComponent(id)}/publish`, { revision }, intakeTemplateSchema); }
  updateTemplate(id: string, name: string, active: boolean, revision: string) { return this.write(`/api/v1/tenant/intake/templates/${encodeURIComponent(id)}`, { name, active, revision }, intakeTemplateSchema, "PATCH"); }
  getBinding(serviceId: string, signal?: AbortSignal) { return this.get(`/api/v1/tenant/intake/services/${encodeURIComponent(serviceId)}`, intakeBindingSchema, signal); }
  setBinding(serviceId: string, templateId: string | null, revision: string) { return this.write(`/api/v1/tenant/intake/services/${encodeURIComponent(serviceId)}`, { templateId, revision }, intakeBindingSchema, "PUT"); }
  statusBatch(bookingIds: string[], signal?: AbortSignal) { return this.write("/api/v1/tenant/intake/bookings/statuses", bookingIds, intakeStatusBatchSchema, "POST", signal); }
  staffStatuses(bookingId: string, signal?: AbortSignal) { return this.get(`/api/v1/tenant/intake/bookings/${encodeURIComponent(bookingId)}`, intakeStatusesSchema, signal); }
  review(bookingId: string, assignmentId: string, signal?: AbortSignal) { return this.get(`/api/v1/tenant/intake/bookings/${encodeURIComponent(bookingId)}/forms/${encodeURIComponent(assignmentId)}/submission`, intakeViewSchema, signal); }
  customerStatuses(bookingId: string, signal?: AbortSignal) { return this.get(`/api/v1/bookings/${encodeURIComponent(bookingId)}/intake`, intakeStatusesSchema, signal); }
  customerRead(bookingId: string, assignmentId: string, signal?: AbortSignal) { return this.get(`/api/v1/bookings/${encodeURIComponent(bookingId)}/intake/${encodeURIComponent(assignmentId)}`, intakeViewSchema, signal); }
  submit(bookingId: string, assignmentId: string, versionId: string, answers: IntakeAnswer[], key: string, signal?: AbortSignal) { return this.write(`/api/v1/bookings/${encodeURIComponent(bookingId)}/intake/${encodeURIComponent(assignmentId)}/submission`, { versionId, answers }, intakeStatusSchema, "POST", signal, key); }
  privacyExport(customerId: string, signal?: AbortSignal) { return this.get(`/api/v1/admin/customers/${encodeURIComponent(customerId)}/intake-export`, intakeViewsSchema, signal); }
  async placeHold(tenantId: string, submissionId: string, category: "legalProceeding" | "regulatory" | "otherLegalRequirement"): Promise<void> {
    await this.client.request(`/api/v1/admin/intake/tenants/${encodeURIComponent(tenantId)}/submissions/${encodeURIComponent(submissionId)}/hold`, { method: "POST", cache: "no-store", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ category }) });
  }
  async releaseHold(tenantId: string, submissionId: string): Promise<void> {
    await this.client.request(`/api/v1/admin/intake/tenants/${encodeURIComponent(tenantId)}/submissions/${encodeURIComponent(submissionId)}/hold/release`, { method: "POST", cache: "no-store" });
  }
}
