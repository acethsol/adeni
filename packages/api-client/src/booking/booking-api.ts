import {
  type PagedResult,
  bookingResponseSchema,
  collectBalanceResponseSchema,
  markBookingPaidRequestSchema,
  createBookingRequestSchema,
  customerBookingsResponseSchema,
  customerBookingResponseSchema,
  tenantBookingsResponseSchema,
  createReviewRequestSchema,
  joinWaitlistRequestSchema,
  reviewResponseSchema,
  weeklyAvailabilityResponseSchema,
  businessClosedDateSchema,
  businessClosedDatesResponseSchema,
  createBusinessClosedDateRequestSchema,
  type BookingResponse,
  type CollectBalanceResponse,
  type MarkBookingPaidRequest,
  type CreateBookingRequest,
  type CustomerBookingResponse,
  type WeeklyAvailabilityRule,
  type BusinessClosedDate,
  type CreateBusinessClosedDateRequest,
  type JoinWaitlistRequest,
  type CreateReviewRequest,
  type ReviewResponse,
  appendListSortParams,
  type ListSortParams,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class BookingApi {
  constructor(private readonly client: ApiClientContext) {}

  async createBooking(
    request: CreateBookingRequest,
    idempotencyKey?: string,
  ): Promise<BookingResponse> {
    const body = createBookingRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/bookings", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      idempotencyKey,
    });
    return bookingResponseSchema.parse(await response.json());
  }

  async getMyBookings(): Promise<CustomerBookingResponse[]> {
    const response = await this.client.request("/api/v1/bookings");
    const payload = customerBookingsResponseSchema.parse(await response.json());
    return payload.items;
  }

  async cancelMyBooking(bookingId: string): Promise<CustomerBookingResponse> {
    const response = await this.client.request(
      `/api/v1/bookings/${encodeURIComponent(bookingId)}/cancel`,
      { method: "POST" },
    );
    return customerBookingResponseSchema.parse(await response.json());
  }

  async createBookingReview(
    bookingId: string,
    request: CreateReviewRequest,
  ): Promise<ReviewResponse> {
    const body = createReviewRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/bookings/${encodeURIComponent(bookingId)}/review`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return reviewResponseSchema.parse(await response.json());
  }

  async listTenantBookings(options: {
    tab?: "pending" | "upcoming" | "past";
    page?: number;
    pageSize?: number;
    sort?: ListSortParams;
    locationId?: string;
  } = {}): Promise<PagedResult<BookingResponse>> {
    const query = new URLSearchParams();
    if (options.locationId) {
      query.set("locationId", options.locationId);
    }
    if (options.tab) {
      query.set("tab", options.tab);
    }
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    appendListSortParams(query, options.sort);
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/tenant/bookings${suffix}`);
    return tenantBookingsResponseSchema.parse(await response.json());
  }

  async acceptTenantBooking(bookingId: string): Promise<BookingResponse> {
    const response = await this.client.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/accept`,
      { method: "POST" },
    );
    return bookingResponseSchema.parse(await response.json());
  }

  async rejectTenantBooking(
    bookingId: string,
    reason?: string,
  ): Promise<BookingResponse> {
    const response = await this.client.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/reject`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ reason: reason ?? null }),
      },
    );
    return bookingResponseSchema.parse(await response.json());
  }

  async checkInTenantBooking(bookingId: string): Promise<BookingResponse> {
    const response = await this.client.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/check-in`,
      { method: "POST" },
    );
    return bookingResponseSchema.parse(await response.json());
  }

  async completeTenantBooking(bookingId: string): Promise<BookingResponse> {
    const response = await this.client.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/complete`,
      { method: "POST" },
    );
    return bookingResponseSchema.parse(await response.json());
  }

  async markTenantBookingPaid(
    bookingId: string,
    request: MarkBookingPaidRequest = {},
  ): Promise<BookingResponse> {
    const body = markBookingPaidRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/mark-paid`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return bookingResponseSchema.parse(await response.json());
  }

  async collectTenantBookingBalance(
    bookingId: string,
  ): Promise<CollectBalanceResponse> {
    const response = await this.client.request(
      `/api/v1/tenant/bookings/${encodeURIComponent(bookingId)}/collect-balance`,
      { method: "POST" },
    );
    return collectBalanceResponseSchema.parse(await response.json());
  }

  async joinWaitlist(request: JoinWaitlistRequest): Promise<{ id: string }> {
    const body = joinWaitlistRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/bookings/waitlist", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return (await response.json()) as { id: string };
  }

  async getTenantAvailability(locationId?: string): Promise<WeeklyAvailabilityRule[]> {
    const query = locationId
      ? `?locationId=${encodeURIComponent(locationId)}`
      : "";
    const response = await this.client.request(`/api/v1/tenant/availability${query}`);
    const payload = weeklyAvailabilityResponseSchema.parse(await response.json());
    return payload.items;
  }

  async replaceTenantAvailability(
    rules: WeeklyAvailabilityRule[],
    locationId?: string,
  ): Promise<WeeklyAvailabilityRule[]> {
    const query = locationId
      ? `?locationId=${encodeURIComponent(locationId)}`
      : "";
    const response = await this.client.request(`/api/v1/tenant/availability${query}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ items: rules }),
    });
    const payload = weeklyAvailabilityResponseSchema.parse(await response.json());
    return payload.items;
  }

  async getTenantClosedDates(locationId?: string): Promise<BusinessClosedDate[]> {
    const query = locationId
      ? `?locationId=${encodeURIComponent(locationId)}`
      : "";
    const response = await this.client.request(`/api/v1/tenant/closed-dates${query}`);
    return businessClosedDatesResponseSchema.parse(await response.json()).items;
  }

  async createTenantClosedDate(
    request: CreateBusinessClosedDateRequest,
    locationId?: string,
  ): Promise<BusinessClosedDate> {
    const body = createBusinessClosedDateRequestSchema.parse(request);
    const query = locationId
      ? `?locationId=${encodeURIComponent(locationId)}`
      : "";
    const response = await this.client.request(`/api/v1/tenant/closed-dates${query}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return businessClosedDateSchema.parse(await response.json());
  }

  async deleteTenantClosedDate(id: string): Promise<void> {
    await this.client.request(`/api/v1/tenant/closed-dates/${encodeURIComponent(id)}`, {
      method: "DELETE",
    });
  }
}
