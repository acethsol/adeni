import {
  type PagedResult,
  mediaUploadUrlRequestSchema,
  mediaUploadUrlResponseSchema,
  createConversationRequestSchema,
  sendMessageRequestSchema,
  conversationSummarySchema,
  conversationDetailSchema,
  conversationsResponseSchema,
  conversationListResponseSchema,
  conversationMessageSchema,
  unreadCountResponseSchema,
  whatsAppLinkResponseSchema,
  messageTemplatesResponseSchema,
  notificationPreferencesSchema,
  updateNotificationPreferencesRequestSchema,
  messagingSettingsSchema,
  updateMessagingSettingsRequestSchema,
  type CreateConversationRequest,
  type SendMessageRequest,
  type ConversationSummary,
  type ConversationDetail,
  type ConversationMessage,
  type WhatsAppLinkResponse,
  type MessageTemplate,
  type NotificationPreferences,
  type UpdateNotificationPreferencesRequest,
  type MessagingSettings,
  type UpdateMessagingSettingsRequest,
  type MediaUploadUrlResponse,
} from "@adeni/shared";
import type { ApiClientContext } from "../core/api-transport";

export class MessagingApi {
  constructor(private readonly client: ApiClientContext) {}

  async createMessageAttachmentUploadUrl(
    contentType: string,
    contentLength: number,
  ): Promise<MediaUploadUrlResponse> {
    const body = mediaUploadUrlRequestSchema.parse({
      purpose: "message_attachment",
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

  async createConversation(
    request: CreateConversationRequest,
  ): Promise<ConversationSummary> {
    const body = createConversationRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/messages/conversations", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return conversationSummarySchema.parse(await response.json());
  }

  async listCustomerConversations(): Promise<ConversationSummary[]> {
    const response = await this.client.request("/api/v1/messages/conversations");
    const payload = conversationListResponseSchema.parse(await response.json());
    return payload.items;
  }

  async getCustomerConversation(
    id: string,
    options: { beforeId?: string; take?: number } = {},
  ): Promise<ConversationDetail> {
    const query = new URLSearchParams();
    if (options.beforeId) query.set("beforeId", options.beforeId);
    if (options.take != null) query.set("take", String(options.take));
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(
      `/api/v1/messages/conversations/${encodeURIComponent(id)}${suffix}`,
    );
    return conversationDetailSchema.parse(await response.json());
  }

  async sendCustomerMessage(
    conversationId: string,
    request: SendMessageRequest,
  ): Promise<ConversationMessage> {
    const body = sendMessageRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/messages/conversations/${encodeURIComponent(conversationId)}/messages`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return conversationMessageSchema.parse(await response.json());
  }

  async markCustomerConversationRead(
    conversationId: string,
    messageId?: string,
  ): Promise<void> {
    const query = messageId ? `?messageId=${encodeURIComponent(messageId)}` : "";
    await this.client.request(
      `/api/v1/messages/conversations/${encodeURIComponent(conversationId)}/read${query}`,
      { method: "POST" },
    );
  }

  async listTenantConversations(options: {
    page?: number;
    pageSize?: number;
    locationId?: string;
  } = {}): Promise<PagedResult<ConversationSummary>> {
    const query = new URLSearchParams();
    if (options.page != null) {
      query.set("page", String(options.page));
    }
    if (options.pageSize != null) {
      query.set("pageSize", String(options.pageSize));
    }
    if (options.locationId) {
      query.set("locationId", options.locationId);
    }
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(`/api/v1/tenant/messages/conversations${suffix}`);
    return conversationsResponseSchema.parse(await response.json());
  }

  async getTenantMessageUnreadCount(locationId?: string): Promise<number> {
    const query = locationId ? `?locationId=${encodeURIComponent(locationId)}` : "";
    const response = await this.client.request(`/api/v1/tenant/messages/unread-count${query}`);
    const payload = unreadCountResponseSchema.parse(await response.json());
    return payload.count;
  }

  async getTenantConversation(
    id: string,
    options: { beforeId?: string; take?: number } = {},
  ): Promise<ConversationDetail> {
    const query = new URLSearchParams();
    if (options.beforeId) query.set("beforeId", options.beforeId);
    if (options.take != null) query.set("take", String(options.take));
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    const response = await this.client.request(
      `/api/v1/tenant/messages/conversations/${encodeURIComponent(id)}${suffix}`,
    );
    return conversationDetailSchema.parse(await response.json());
  }

  async sendTenantMessage(
    conversationId: string,
    request: SendMessageRequest,
  ): Promise<ConversationMessage> {
    const body = sendMessageRequestSchema.parse(request);
    const response = await this.client.request(
      `/api/v1/tenant/messages/conversations/${encodeURIComponent(conversationId)}/messages`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      },
    );
    return conversationMessageSchema.parse(await response.json());
  }

  async markTenantConversationRead(
    conversationId: string,
    messageId?: string,
  ): Promise<void> {
    const query = messageId ? `?messageId=${encodeURIComponent(messageId)}` : "";
    await this.client.request(
      `/api/v1/tenant/messages/conversations/${encodeURIComponent(conversationId)}/read${query}`,
      { method: "POST" },
    );
  }

  async getTenantMessageTemplates(): Promise<MessageTemplate[]> {
    const response = await this.client.request("/api/v1/tenant/messages/templates");
    const payload = messageTemplatesResponseSchema.parse(await response.json());
    return payload.items;
  }

  async getNotificationPreferences(): Promise<NotificationPreferences> {
    const response = await this.client.request("/api/v1/tenant/notification-preferences");
    return notificationPreferencesSchema.parse(await response.json());
  }

  async updateNotificationPreferences(
    request: UpdateNotificationPreferencesRequest,
  ): Promise<NotificationPreferences> {
    const body = updateNotificationPreferencesRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/notification-preferences", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return notificationPreferencesSchema.parse(await response.json());
  }

  async getMessagingSettings(): Promise<MessagingSettings> {
    const response = await this.client.request("/api/v1/tenant/messages/settings");
    return messagingSettingsSchema.parse(await response.json());
  }

  async updateMessagingSettings(
    request: UpdateMessagingSettingsRequest,
  ): Promise<MessagingSettings> {
    const body = updateMessagingSettingsRequestSchema.parse(request);
    const response = await this.client.request("/api/v1/tenant/messages/settings", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return messagingSettingsSchema.parse(await response.json());
  }

  async getBusinessWhatsAppLink(slug: string): Promise<WhatsAppLinkResponse> {
    const response = await this.client.request(
      `/api/v1/businesses/${encodeURIComponent(slug)}/whatsapp-link`,
    );
    return whatsAppLinkResponseSchema.parse(await response.json());
  }

  async getBookingWhatsAppLink(bookingId: string): Promise<WhatsAppLinkResponse> {
    const response = await this.client.request(
      `/api/v1/bookings/${encodeURIComponent(bookingId)}/whatsapp-link`,
    );
    return whatsAppLinkResponseSchema.parse(await response.json());
  }
}
