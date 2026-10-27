import { Component, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Subscription } from "rxjs";
import {
  DEFAULT_PAGE_SIZE,
  formatSlotTime,
  LIST_PAGE_SIZE_OPTIONS,
  MAX_UPLOAD_BYTES,
  type BusinessProfile,
  type ConversationDetail,
  type ConversationMessage,
  type ConversationSummary,
  type MessageTemplate,
  type SendMessageAttachment,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniListPaginationComponent,
  AdeniModalComponent,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
import { MessagingRealtimeService } from "@adeni/ui";
import { PortalNavBadgesService } from "../../core/services/portal-nav-badges.service";
import { mergeTenantEntitlements } from "@adeni/shared";

const ALLOWED_ATTACHMENT_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);

@Component({
  selector: "app-messages",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniPortalEmptyStateComponent,
    AdeniCarbonIconComponent,
    AdeniModalComponent,
    AdeniListPaginationComponent,
  ],
  templateUrl: "./messages.component.html",
  styleUrl: "./messages.component.scss",
})
export class MessagesComponent implements OnInit, OnDestroy {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly realtime = inject(MessagingRealtimeService);
  private readonly navBadges = inject(PortalNavBadgesService);
  private readonly activeLocation = inject(PortalActiveLocationService);
  private readonly subscriptions = new Subscription();

  readonly profile = signal<BusinessProfile | null>(null);
  readonly whatsAppBusinessUrl = signal<string | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly conversations = signal<ConversationSummary[]>([]);
  readonly conversationTotalCount = signal(0);
  readonly conversationPage = signal(1);
  readonly conversationPageSize = signal(DEFAULT_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;
  readonly templates = signal<MessageTemplate[]>([]);
  readonly detail = signal<ConversationDetail | null>(null);
  readonly sending = signal(false);
  readonly attaching = signal(false);
  readonly pendingAttachment = signal<SendMessageAttachment | null>(null);
  readonly pendingAttachmentPreview = signal<string | null>(null);
  readonly imagePreview = signal<{ url: string; fileName: string } | null>(null);
  /** Customer read cursor — when set, outgoing messages at or before this id show as read. */
  readonly customerLastReadMessageId = signal<string | null>(null);
  draft = "";

  readonly formatSlotTime = formatSlotTime;
  readonly formatMessageTime = formatMessageTime;

  ngOnInit(): void {
    this.subscriptions.add(
      this.realtime.messageCreated$.subscribe((message) => this.onRealtimeMessage(message)),
    );
    this.subscriptions.add(
      this.realtime.conversationUpdated$.subscribe((summary) => this.onRealtimeConversation(summary)),
    );
    this.subscriptions.add(
      this.realtime.readUpdated$.subscribe((event) => this.onReadUpdated(event)),
    );
    void this.load();
    this.subscriptions.add(
      this.activeLocation.selectionChanged$.subscribe(() => {
        void this.activeLocation.runOnBranchSwitch(() => this.load());
      }),
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.clearPendingAttachment();
    void this.realtime.stop();
  }

  canSend(): boolean {
    return Boolean(this.draft.trim() || this.pendingAttachment()) && !this.sending() && !this.attaching();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
      const snapshot = await this.api.withAuthorizedClient(async (client) => {
        const profile = await client.getTenantProfile();
        let conversations: ConversationSummary[] = [];
        let templates: MessageTemplate[] = [];
        let conversationTotalCount = 0;
        let conversationPage = 1;
        let whatsAppBusinessUrl: string | null = null;
        const slug = primaryLocationSlug(profile);
        if (slug) {
          try {
            const link = await client.getBusinessWhatsAppLink(slug);
            whatsAppBusinessUrl = link.url;
          } catch {
            whatsAppBusinessUrl = null;
          }
        }
        const planEntitlements = await client.getTenantEntitlements().catch(() => null);
        const mergedEntitlements = mergeTenantEntitlements(
          planEntitlements,
          profile.entitlements,
          profile.entitlements,
        );
        const locationQueryId = this.activeLocation.tenantApiLocationId(mergedEntitlements);
        const [conversationPageResult, templateList] = await Promise.all([
          client.listTenantConversations({
            page: this.conversationPage(),
            pageSize: this.conversationPageSize(),
            locationId: locationQueryId,
          }),
          client.getTenantMessageTemplates(),
        ]);
        conversations = conversationPageResult.items;
        conversationTotalCount = conversationPageResult.totalCount;
        conversationPage = conversationPageResult.page;
        const resolvedPageSize = conversationPageResult.pageSize;
        templates = templateList;
        return {
          conversations,
          templates,
          profile,
          conversationTotalCount,
          conversationPage,
          resolvedPageSize,
          whatsAppBusinessUrl,
        };
      });
      this.conversations.set(snapshot.conversations);
      this.conversationTotalCount.set(snapshot.conversationTotalCount);
      this.conversationPage.set(snapshot.conversationPage);
      this.conversationPageSize.set(snapshot.resolvedPageSize);
      this.templates.set(snapshot.templates);
      this.profile.set(snapshot.profile);
      this.whatsAppBusinessUrl.set(snapshot.whatsAppBusinessUrl);

      if (snapshot.profile.tenantId) {
        try {
          await this.realtime.start(snapshot.profile.tenantId);
        } catch {
          // REST inbox still works if realtime is unavailable.
        }
      }
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.messages.couldNotLoadMessages"));
    } finally {
      this.loading.set(false);
    }
  }

  goToConversationPage(next: number): void {
    if (next === this.conversationPage()) {
      return;
    }
    this.conversationPage.set(next);
    this.detail.set(null);
    void this.load();
  }

  onConversationPageSizeChange(size: number): void {
    this.conversationPageSize.set(size);
    this.conversationPage.set(1);
    this.detail.set(null);
    void this.load();
  }

  async openConversation(conversation: ConversationSummary): Promise<void> {
    this.error.set(null);
    this.clearPendingAttachment();
    this.customerLastReadMessageId.set(null);
    try {
      const detail = await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient(async (client) => {
            const loaded = await client.getTenantConversation(conversation.id);
            await client.markTenantConversationRead(conversation.id);
            return loaded;
          }),
        this.tr("portal.messages.openingConversation"),
        this.tr("portal.messages.loadingMessages2"),
      );
      this.detail.set(detail);
      this.conversations.update((items) =>
        items.map((item) =>
          item.id === conversation.id ? { ...item, needsAttention: false } : item,
        ),
      );
      try {
        await this.realtime.joinConversation(conversation.id);
      } catch {
        // Opened via REST; live updates may be delayed until reconnect.
      }
      this.navBadges.requestRefresh();
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.messages.couldNotOpenConversation"));
    }
  }

  async onAttachmentSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.detail()) {
      return;
    }

    if (!ALLOWED_ATTACHMENT_TYPES.has(file.type)) {
      this.error.set(this.tr("common.useJpegPngWebp"));
      input.value = "";
      return;
    }
    if (file.size > MAX_UPLOAD_BYTES) {
      this.error.set(this.tr("portal.messages.attachmentMustBe5Mb"));
      input.value = "";
      return;
    }

    this.attaching.set(true);
    this.error.set(null);
    try {
      const attachment = await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient(async (client) => {
            const slot = await client.createMessageAttachmentUploadUrl(file.type, file.size);
            const upload = await fetch(slot.uploadUrl, {
              method: "PUT",
              headers: { "Content-Type": file.type },
              body: file,
            });
            if (!upload.ok) {
              throw new Error("upload failed");
            }
            return {
              storageKey: slot.storageKey,
              contentType: file.type,
              fileName: file.name,
              fileSize: file.size,
            } satisfies SendMessageAttachment;
          }),
        this.tr("portal.messages.uploadingImage"),
        this.tr("portal.messages.preparingAttachment"),
      );
      this.clearPendingAttachment();
      this.pendingAttachment.set(attachment);
      this.pendingAttachmentPreview.set(URL.createObjectURL(file));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.messages.couldNotUploadImage"));
    } finally {
      this.attaching.set(false);
      input.value = "";
    }
  }

  clearPendingAttachment(): void {
    const preview = this.pendingAttachmentPreview();
    if (preview) {
      URL.revokeObjectURL(preview);
    }
    this.pendingAttachment.set(null);
    this.pendingAttachmentPreview.set(null);
  }

  async send(): Promise<void> {
    const conversation = this.detail();
    const body = this.draft.trim();
    const attachment = this.pendingAttachment();
    if (!conversation || (!body && !attachment)) return;
    this.sending.set(true);
    this.error.set(null);
    try {
      const clientMessageId =
        typeof crypto !== "undefined" && "randomUUID" in crypto
          ? crypto.randomUUID()
          : `portal-${Date.now()}`;
      const message = await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((client) =>
            client.sendTenantMessage(conversation.id, {
              body: body || undefined,
              clientMessageId,
              attachments: attachment ? [attachment] : undefined,
            }),
          ),
        this.tr("portal.messages.sendingMessage"),
        this.tr("portal.messages.deliveringCustomer"),
      );
      this.appendMessage(message);
      this.draft = "";
      this.clearPendingAttachment();
      this.conversations.update((items) =>
        items.map((item) =>
          item.id === conversation.id
            ? {
                ...item,
                preview: message.body?.trim() || (message.attachments?.length ? this.tr("portal.messages.attachment") : item.preview),
                lastMessageAt: message.createdAt,
                needsAttention: false,
              }
            : item,
        ),
      );
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.messages.couldNotSendMessage"));
    } finally {
      this.sending.set(false);
    }
  }

  useTemplate(template: MessageTemplate): void {
    this.draft = template.body;
  }

  openImagePreview(url: string, fileName: string): void {
    this.imagePreview.set({ url, fileName });
  }

  closeImagePreview(): void {
    this.imagePreview.set(null);
  }

  isMessageReadByCustomer(message: ConversationMessage): boolean {
    const cursor = this.customerLastReadMessageId();
    if (!cursor) {
      return false;
    }
    return message.id.localeCompare(cursor) <= 0;
  }

  private onReadUpdated(event: {
    conversationId: string;
    participantId: string;
    lastReadMessageId: string;
  }): void {
    if (this.detail()?.id !== event.conversationId) {
      return;
    }
    this.customerLastReadMessageId.set(event.lastReadMessageId);
  }

  senderLabel(message: ConversationMessage, conversation: ConversationDetail): string {
    if (message.senderDisplayName?.trim()) {
      return message.senderDisplayName.trim();
    }
    if (message.senderType === "business" || message.senderType === "bot") {
      return conversation.businessName?.trim() || this.tr("portal.messages.you");
    }
    return conversation.customerDisplayName;
  }

  private onRealtimeMessage(message: ConversationMessage): void {
    const open = this.detail();
    if (!open || open.id !== message.conversationId) {
      return;
    }
    this.appendMessage(message);
  }

  private onRealtimeConversation(summary: ConversationSummary): void {
    const openId = this.detail()?.id;
    const next = openId === summary.id ? { ...summary, needsAttention: false } : summary;
    this.conversations.update((items) => {
      const index = items.findIndex((item) => item.id === next.id);
      if (index < 0) {
        return [next, ...items].slice(0, this.conversationPageSize());
      }
      const copy = [...items];
      copy[index] = next;
      copy.sort((a, b) => b.lastMessageAt.localeCompare(a.lastMessageAt));
      return copy;
    });
  }

  private appendMessage(message: ConversationMessage): void {
    this.detail.update((current) => {
      if (!current || current.id !== message.conversationId) {
        return current;
      }
      if (current.messages.some((existing) => existing.id === message.id)) {
        return current;
      }
      if (
        message.clientMessageId &&
        current.messages.some((existing) => existing.clientMessageId === message.clientMessageId)
      ) {
        return current;
      }
      return { ...current, messages: [...current.messages, message] };
    });
  }
}

function formatMessageTime(iso: string): string {
  try {
    const date = new Date(iso);
    const now = new Date();
    const sameDay =
      date.getFullYear() === now.getFullYear()
      && date.getMonth() === now.getMonth()
      && date.getDate() === now.getDate();
    if (sameDay) {
      return new Intl.DateTimeFormat(undefined, {
        hour: "numeric",
        minute: "2-digit",
      }).format(date);
    }
    return new Intl.DateTimeFormat(undefined, {
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
    }).format(date);
  } catch {
    return iso;
  }
}

function primaryLocationSlug(profile: BusinessProfile): string | null {
  const primary = profile.locations.find((location) => location.isPrimary) ?? profile.locations[0];
  return primary?.slug ?? null;
}
