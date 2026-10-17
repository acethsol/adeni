import { Component, computed, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router } from "@angular/router";
import { FormsModule } from "@angular/forms";
import { map, Subscription } from "rxjs";
import {
  formatSlotTime,
  hasTenantEntitlement,
  MAX_UPLOAD_BYTES,
  pageRangeLabel,
  totalPages as computeTotalPages,
  type BusinessProfile,
  type ConversationDetail,
  type ConversationMessage,
  type ConversationSummary,
  type MessageTemplate,
  type SendMessageAttachment,
} from "@adeni/shared";
import { AdeniPortalEmptyStateComponent, PortalPageComponent } from "@adeni/ui";
import { filterPortalTabs } from "../../core/portal-access";
import { findPortalNavItem } from "../../core/portal-nav";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";
import { MessagingRealtimeService } from "../../core/services/messaging-realtime.service";
import { PortalTabsComponent } from "../../shared/portal-tabs.component";

const ALLOWED_ATTACHMENT_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);

@Component({
  selector: "app-messages",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, PortalTabsComponent, AdeniPortalEmptyStateComponent],
  templateUrl: "./messages.component.html",
  styleUrl: "./messages.component.scss",
})
export class MessagesComponent implements OnInit, OnDestroy {
  private readonly api = inject(BusinessApiService);
  private readonly realtime = inject(MessagingRealtimeService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly subscriptions = new Subscription();

  readonly tab = toSignal(this.route.queryParamMap.pipe(map((params) => params.get("tab") ?? "whatsapp")), {
    initialValue: this.route.snapshot.queryParamMap.get("tab") ?? "whatsapp",
  });

  private readonly navTabs = findPortalNavItem("/messages")?.item.tabs ?? [
    { id: "inbox", label: "Inbox", entitlement: "messaging" as const },
    { id: "whatsapp", label: "WhatsApp" },
  ];

  readonly profile = signal<BusinessProfile | null>(null);
  readonly visibleTabs = computed(() =>
    filterPortalTabs(this.navTabs, this.profile()?.entitlements ?? null),
  );
  readonly messagingEntitled = computed(() =>
    hasTenantEntitlement(this.profile()?.entitlements, "messaging"),
  );

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly conversations = signal<ConversationSummary[]>([]);
  readonly conversationTotalCount = signal(0);
  readonly conversationPage = signal(1);
  readonly conversationPageSize = 25;
  readonly conversationTotalPageCount = computed(() =>
    computeTotalPages(this.conversationTotalCount(), this.conversationPageSize),
  );
  readonly conversationPageLabel = computed(() =>
    pageRangeLabel(this.conversationPage(), this.conversationPageSize, this.conversationTotalCount()),
  );
  readonly templates = signal<MessageTemplate[]>([]);
  readonly detail = signal<ConversationDetail | null>(null);
  readonly sending = signal(false);
  readonly attaching = signal(false);
  readonly pendingAttachment = signal<SendMessageAttachment | null>(null);
  readonly pendingAttachmentPreview = signal<string | null>(null);
  draft = "";

  readonly formatSlotTime = formatSlotTime;
  readonly canSend = computed(
    () => Boolean(this.draft.trim() || this.pendingAttachment()) && !this.sending() && !this.attaching(),
  );

  ngOnInit(): void {
    this.subscriptions.add(
      this.realtime.messageCreated$.subscribe((message) => this.onRealtimeMessage(message)),
    );
    this.subscriptions.add(
      this.realtime.conversationUpdated$.subscribe((summary) => this.onRealtimeConversation(summary)),
    );
    void this.load();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.clearPendingAttachment();
    void this.realtime.stop();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const snapshot = await this.api.withAuthorizedClient(async (client) => {
        const profile = await client.getTenantProfile();
        let conversations: ConversationSummary[] = [];
        let templates: MessageTemplate[] = [];
        let conversationTotalCount = 0;
        let conversationPage = 1;
        if (hasTenantEntitlement(profile.entitlements, "messaging")) {
          const [conversationPageResult, templateList] = await Promise.all([
            client.listTenantConversations({
              page: this.conversationPage(),
              pageSize: this.conversationPageSize,
            }),
            client.getTenantMessageTemplates(),
          ]);
          conversations = conversationPageResult.items;
          conversationTotalCount = conversationPageResult.totalCount;
          conversationPage = conversationPageResult.page;
          templates = templateList;
        }
        return { conversations, templates, profile, conversationTotalCount, conversationPage };
      });
      this.conversations.set(snapshot.conversations);
      this.conversationTotalCount.set(snapshot.conversationTotalCount);
      this.conversationPage.set(snapshot.conversationPage);
      this.templates.set(snapshot.templates);
      this.profile.set(snapshot.profile);
      if (!this.route.snapshot.queryParamMap.get("tab")) {
        const defaultTab = hasTenantEntitlement(snapshot.profile.entitlements, "messaging")
          ? "inbox"
          : "whatsapp";
        await this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { tab: defaultTab },
          replaceUrl: true,
        });
      }

      if (hasTenantEntitlement(snapshot.profile.entitlements, "messaging") && snapshot.profile.tenantId) {
        try {
          await this.realtime.start(snapshot.profile.tenantId);
        } catch {
          // REST inbox still works if realtime is unavailable.
        }
      }
    } catch {
      this.error.set("Could not load messages.");
    } finally {
      this.loading.set(false);
    }
  }

  canNextConversationPage(): boolean {
    return this.conversationPage() < this.conversationTotalPageCount();
  }

  prevConversationPage(): void {
    if (this.conversationPage() <= 1) {
      return;
    }
    this.conversationPage.update((p) => p - 1);
    this.detail.set(null);
    void this.load();
  }

  nextConversationPage(): void {
    if (!this.canNextConversationPage()) {
      return;
    }
    this.conversationPage.update((p) => p + 1);
    this.detail.set(null);
    void this.load();
  }

  async openConversation(conversation: ConversationSummary): Promise<void> {
    this.error.set(null);
    this.clearPendingAttachment();
    try {
      const detail = await this.api.withAuthorizedClient(async (client) => {
        const loaded = await client.getTenantConversation(conversation.id);
        await client.markTenantConversationRead(conversation.id);
        return loaded;
      });
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
    } catch {
      this.error.set("Could not open that conversation.");
    }
  }

  async onAttachmentSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.detail()) {
      return;
    }

    if (!ALLOWED_ATTACHMENT_TYPES.has(file.type)) {
      this.error.set("Use JPEG, PNG, or WebP.");
      input.value = "";
      return;
    }
    if (file.size > MAX_UPLOAD_BYTES) {
      this.error.set("Attachment must be 5 MB or smaller.");
      input.value = "";
      return;
    }

    this.attaching.set(true);
    this.error.set(null);
    try {
      const attachment = await this.api.withAuthorizedClient(async (client) => {
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
      });
      this.clearPendingAttachment();
      this.pendingAttachment.set(attachment);
      this.pendingAttachmentPreview.set(URL.createObjectURL(file));
    } catch {
      this.error.set("Could not upload that image.");
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
      const message = await this.api.withAuthorizedClient((client) =>
        client.sendTenantMessage(conversation.id, {
          body: body || undefined,
          clientMessageId,
          attachments: attachment ? [attachment] : undefined,
        }),
      );
      this.appendMessage(message);
      this.draft = "";
      this.clearPendingAttachment();
      this.conversations.update((items) =>
        items.map((item) =>
          item.id === conversation.id
            ? {
                ...item,
                preview: message.body?.trim() || (message.attachments?.length ? "Attachment" : item.preview),
                lastMessageAt: message.createdAt,
                needsAttention: false,
              }
            : item,
        ),
      );
    } catch {
      this.error.set("Could not send that message.");
    } finally {
      this.sending.set(false);
    }
  }

  useTemplate(template: MessageTemplate): void {
    this.draft = template.body;
  }

  primarySlug(): string | null {
    const profile = this.profile();
    if (!profile) return null;
    const primary = profile.locations.find((location) => location.isPrimary) ?? profile.locations[0];
    return primary?.slug ?? null;
  }

  whatsAppUrl(message: string): string {
    return `https://wa.me/?text=${encodeURIComponent(message)}`;
  }

  whatsAppNotes(): { title: string; body: string }[] {
    const name = this.profile()?.businessName ?? "us";
    const link = this.primarySlug()
      ? `${this.config.discoverWebUrl}/businesses/${this.primarySlug()}`
      : this.config.discoverWebUrl;
    return [
      { title: "Booking confirmed", body: `Hi! Your booking with ${name} is confirmed. ${link}` },
      { title: "Reminder", body: `Reminder from ${name}: we look forward to seeing you. ${link}` },
      { title: "Thank you", body: `Thanks for visiting ${name}. Book again anytime: ${link}` },
    ];
  }

  async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.notice.set("Copied.");
    } catch {
      this.error.set("Could not copy.");
    }
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
        return [next, ...items].slice(0, this.conversationPageSize);
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
