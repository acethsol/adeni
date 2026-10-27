import {
  authSessionSchema,
  businessContextResponseSchema,
  businessLocationSchema,
  businessProfileSchema,
  registerBusinessRequestSchema,
  registerBusinessResponseSchema,
  submitVerificationRequestSchema,
  tenantLocationsResponseSchema,
  updateBusinessProfileRequestSchema,
  upsertBusinessLocationRequestSchema,
  mediaUploadUrlRequestSchema,
  mediaUploadUrlResponseSchema,
  updateCoverImageRequestSchema,
  updateCoverImageResponseSchema,
  addGalleryImageRequestSchema,
  removeGalleryImageRequestSchema,
  galleryImagesResponseSchema,
  replyToReviewRequestSchema,
  requestVerificationBadgeRequestSchema,
  verificationBadgeSchema,
  publicReviewItemSchema,
  tenantReviewsListResponseSchema,
  PROFILE_REVIEWS_PAGE_SIZE,
  type PagedResult,
  updateBusinessSettingsRequestSchema,
  updateBusinessPoliciesRequestSchema,
  updatePublicPageRequestSchema,
  type AuthSession,
  type BusinessContextResponse,
  type BusinessProfile,
  type BusinessLocation,
  type RegisterBusinessRequest,
  type RegisterBusinessResponse,
  type SubmitVerificationRequest,
  type UpdateBusinessProfileRequest,
  type UpdateBusinessSettingsRequest,
  type UpdateBusinessPoliciesRequest,
  type UpdatePublicPageRequest,
  type RequestVerificationBadgeRequest,
  type VerificationBadge,
  type PublicReviewItem,
  type UpdateCoverImageRequest,
  type AddGalleryImageRequest,
  type RemoveGalleryImageRequest,
  type GalleryImage,
  type MediaUploadUrlResponse,
  type UpsertBusinessLocationRequest,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class BusinessApi {
  constructor(private readonly client: ApiClientContext) {}

  async getTenantProfile(): Promise<BusinessProfile> {
    const response = await this.client.request("/api/v1/tenant/profile");
    return businessProfileSchema.parse(await response.json());
  }

  async getBusinessContext(): Promise<BusinessContextResponse> {
    const response = await this.client.request("/api/v1/tenant/context");
    return businessContextResponseSchema.parse(await response.json());
  }

  async registerBusiness(
    request: RegisterBusinessRequest,
  ): Promise<RegisterBusinessResponse> {
    const body = registerBusinessRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/register", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return registerBusinessResponseSchema.parse(await response.json());
  }

  async updateTenantProfile(
    request: UpdateBusinessProfileRequest,
  ): Promise<BusinessProfile> {
    const body = updateBusinessProfileRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/profile", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessProfileSchema.parse(await response.json());
  }

  async updateTenantSettings(
    request: UpdateBusinessSettingsRequest,
  ): Promise<BusinessProfile> {
    const body = updateBusinessSettingsRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/settings", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessProfileSchema.parse(await response.json());
  }

  async updateTenantPublicPage(
    request: UpdatePublicPageRequest,
  ): Promise<BusinessProfile> {
    const body = updatePublicPageRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/public-page", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessProfileSchema.parse(await response.json());
  }

  async updateTenantPolicies(
    request: UpdateBusinessPoliciesRequest,
  ): Promise<BusinessProfile> {
    const body = updateBusinessPoliciesRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/policies", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessProfileSchema.parse(await response.json());
  }

  async listTenantVerificationBadges(): Promise<VerificationBadge[]> {
    const response = await this.client.request("/api/v1/tenant/verification/badges");
    const payload = (await response.json()) as { items: VerificationBadge[] };
    return payload.items ?? [];
  }

  async listTenantReviews(options: {
    page?: number;
    pageSize?: number;
    locationId?: string;
  } = {}): Promise<PagedResult<PublicReviewItem> & { repliedCount?: number }> {
    const query = new URLSearchParams();
    if (options.locationId) {
      query.set("locationId", options.locationId);
    }
    query.set("page", String(options.page ?? 1));
    query.set("pageSize", String(options.pageSize ?? PROFILE_REVIEWS_PAGE_SIZE));
    const response = await this.client.request(`/api/v1/tenant/reviews?${query.toString()}`);
    return tenantReviewsListResponseSchema.parse(await response.json());
  }

  async replyToReview(reviewId: string, reply: string): Promise<PublicReviewItem> {
    const body = replyToReviewRequestSchema.parse({ reply });
    const response = await this.client.request(
      `/api/v1/tenant/reviews/${encodeURIComponent(reviewId)}/reply`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return publicReviewItemSchema.parse(await response.json());
  }

  async requestVerificationBadge(
    request: RequestVerificationBadgeRequest,
  ): Promise<VerificationBadge> {
    const body = requestVerificationBadgeRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/verification/badges", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return verificationBadgeSchema.parse(await response.json());
  }

  async createCoverUploadUrl(
    contentType: string,
    contentLength: number,
  ): Promise<MediaUploadUrlResponse> {
    const body = mediaUploadUrlRequestSchema.parse({
      purpose: "cover",
      contentType,
      contentLength,
    });
    const response = await this.client.request("/api/v1/tenant/media/upload-url", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return mediaUploadUrlResponseSchema.parse(await response.json());
  }

  async createVerificationDocumentUploadUrl(
    contentType: string,
    contentLength: number,
  ): Promise<MediaUploadUrlResponse> {
    const body = mediaUploadUrlRequestSchema.parse({
      purpose: "verification_document",
      contentType,
      contentLength,
    });
    const response = await this.client.request("/api/v1/tenant/media/upload-url", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return mediaUploadUrlResponseSchema.parse(await response.json());
  }

  async createGalleryUploadUrl(
    contentType: string,
    contentLength: number,
  ): Promise<MediaUploadUrlResponse> {
    const body = mediaUploadUrlRequestSchema.parse({
      purpose: "gallery",
      contentType,
      contentLength,
    });
    const response = await this.client.request("/api/v1/tenant/media/upload-url", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return mediaUploadUrlResponseSchema.parse(await response.json());
  }

  async updateTenantCoverImage(request: UpdateCoverImageRequest): Promise<string> {
    const body = updateCoverImageRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/profile/cover", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    const payload = updateCoverImageResponseSchema.parse(await response.json());
    return payload.coverImageUrl;
  }

  async addTenantGalleryImage(request: AddGalleryImageRequest): Promise<GalleryImage[]> {
    const body = addGalleryImageRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/profile/gallery", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return galleryImagesResponseSchema.parse(await response.json()).items;
  }

  async removeTenantGalleryImage(request: RemoveGalleryImageRequest): Promise<GalleryImage[]> {
    const body = removeGalleryImageRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/profile/gallery", {
      method: "DELETE",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return galleryImagesResponseSchema.parse(await response.json()).items;
  }

  async submitTenantVerification(
    request: SubmitVerificationRequest,
  ): Promise<void> {
    const body = submitVerificationRequestSchema.parse(request);
    await this.client.request("/api/v1/tenant/verification", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
  }

  async getTenantLocations(): Promise<BusinessLocation[]> {
    const response = await this.client.request("/api/v1/tenant/locations");
    const payload = tenantLocationsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async addTenantLocation(
    request: UpsertBusinessLocationRequest,
  ): Promise<BusinessLocation> {
    const body = upsertBusinessLocationRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/locations", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessLocationSchema.parse(await response.json());
  }

  async updateTenantLocation(
    locationId: string,
    request: UpsertBusinessLocationRequest,
  ): Promise<BusinessLocation> {
    const body = upsertBusinessLocationRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/locations/${encodeURIComponent(locationId)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return businessLocationSchema.parse(await response.json());
  }

  async deactivateTenantLocation(locationId: string): Promise<void> {
    await this.client.request(
      `/api/v1/tenant/locations/${encodeURIComponent(locationId)}`,
      { method: "DELETE" },
    );
  }

  async getMe(): Promise<AuthSession> {
    const response = await this.client.request("/api/v1/auth/me");
    return authSessionSchema.parse(await response.json());
  }
}
