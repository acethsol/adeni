import {
  afterNextRender,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
  viewChild,
} from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { Subscription } from "rxjs";
import type { ConversationDetail, ConversationMessage, ConversationSummary } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniMessagingRealtimeService,
  AdeniModalComponent,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { CustomerApiService } from "../../core/services/customer-api.service";

@Component({
  selector: "app-my-messages",
  standalone: true,
  imports: [RouterLink, FormsModule, AdeniCarbonIconComponent, AdeniModalComponent],
  templateUrl: "./my-messages.component.html",
  styleUrl: "./my-messages.component.scss",
})
export class MyMessagesComponent implements OnInit, OnDestroy {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(CustomerApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly realtime = inject(AdeniMessagingRealtimeService);
  private readonly subscriptions = new Subscription();

  private readonly transcriptEl = viewChild<ElementRef<HTMLElement>>("transcriptEl");

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly conversations = signal<ConversationSummary[]>([]);
  readonly selectedId = signal<string | null>(null);
  readonly detail = signal<ConversationDetail | null>(null);
  readonly sending = signal(false);
  readonly mobileThreadOpen = signal(false);
  readonly imagePreview = signal<{ url: string; fileName: string } | null>(null);
  draft = "";

  constructor() {
    afterNextRender(() => this.scrollTranscriptToEnd());
  }

  ngOnInit(): void {
    this.subscriptions.add(
      this.realtime.messageCreated$.subscribe((message) => this.onRealtimeMessage(message)),
    );
    void this.loadConversations();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    void this.realtime.stop();
  }

  canSend(): boolean {
    return Boolean(this.draft.trim()) && !this.sending();
  }

  async loadConversations(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const items = await this.api.withAuthorizedClient((c) => c.listCustomerConversations());
      this.conversations.set(items);
      if (!this.selectedId() && items.length > 0 && !this.isNarrow()) {
        await this.openConversation(items[0]!.id);
      }
    } catch (caught) {
      this.error.set(this.apiErr(caught, "discover.myMessages.couldNotLoadMessagesTry"));
      this.conversations.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  async openConversation(id: string): Promise<void> {
    this.selectedId.set(id);
    this.mobileThreadOpen.set(true);
    this.error.set(null);
    try {
      const detail = await this.api.withAuthorizedClient((c) => c.getCustomerConversation(id));
      this.detail.set(detail);
      this.conversations.update((rows) =>
        rows.map((row) => (row.id === id ? { ...row, needsAttention: false } : row)),
      );
      await this.realtime.joinConversation(id);
      await this.api.withAuthorizedClient((c) =>
        c.markCustomerConversationRead(id, detail.messages.at(-1)?.id),
      );
      queueMicrotask(() => this.scrollTranscriptToEnd());
    } catch (caught) {
      this.error.set(this.apiErr(caught, "discover.myMessages.couldNotOpenConversation"));
      this.detail.set(null);
    }
  }

  closeMobileThread(): void {
    this.mobileThreadOpen.set(false);
  }

  async send(): Promise<void> {
    const conversationId = this.selectedId();
    const body = this.draft.trim();
    if (!conversationId || !body || this.sending()) {
      return;
    }

    this.sending.set(true);
    try {
      const message = await this.api.withAuthorizedClient((c) =>
        c.sendCustomerMessage(conversationId, { body }),
      );
      this.draft = "";
      this.appendMessage(message);
      this.bumpConversationPreview(message);
      queueMicrotask(() => this.scrollTranscriptToEnd());
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "discover.myMessages.messageCouldNotBeSent"));
    } finally {
      this.sending.set(false);
    }
  }

  onComposerEnter(event: Event): void {
    const keyEvent = event as KeyboardEvent;
    if (keyEvent.shiftKey) {
      return;
    }
    keyEvent.preventDefault();
    void this.send();
  }

  isOutgoing(message: ConversationMessage): boolean {
    return message.senderType === "customer";
  }

  isBot(message: ConversationMessage): boolean {
    return message.senderType === "bot";
  }

  senderLabel(message: ConversationMessage, conversation: ConversationDetail): string {
    if (this.isOutgoing(message)) {
      return "You";
    }
    if (message.senderDisplayName?.trim()) {
      return message.senderDisplayName.trim();
    }
    if (message.senderType === "bot") {
      return "Adeni FAQ";
    }
    return conversation.businessName?.trim() || "Business";
  }

  initials(name: string | null | undefined): string {
    const parts = (name ?? "B").trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return "B";
    }
    if (parts.length === 1) {
      return parts[0]!.slice(0, 2).toUpperCase();
    }
    return `${parts[0]![0] ?? ""}${parts[1]![0] ?? ""}`.toUpperCase();
  }

  formatListTime(iso: string): string {
    return formatMessageTime(iso);
  }

  formatMessageTime(iso: string): string {
    return formatMessageTime(iso);
  }

  openImagePreview(url: string, fileName: string): void {
    this.imagePreview.set({ url, fileName });
  }

  closeImagePreview(): void {
    this.imagePreview.set(null);
  }

  private onRealtimeMessage(message: ConversationMessage): void {
    this.bumpConversationPreview(message);
    const selected = this.selectedId();
    if (!selected || message.conversationId !== selected) {
      if (message.senderType !== "customer") {
        this.conversations.update((rows) =>
          rows.map((row) =>
            row.id === message.conversationId ? { ...row, needsAttention: true } : row,
          ),
        );
      }
      return;
    }
    this.appendMessage(message);
    queueMicrotask(() => this.scrollTranscriptToEnd());
    if (message.senderType !== "customer") {
      void this.api.withAuthorizedClient((c) =>
        c.markCustomerConversationRead(selected, message.id),
      );
    }
  }

  private bumpConversationPreview(message: ConversationMessage): void {
    const preview = message.body?.trim() || "Attachment";
    this.conversations.update((rows) => {
      const next = rows.map((row) =>
        row.id === message.conversationId
          ? { ...row, preview, lastMessageAt: message.createdAt }
          : row,
      );
      return [...next].sort(
        (a, b) => new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime(),
      );
    });
  }

  private appendMessage(message: ConversationMessage): void {
    const current = this.detail();
    if (!current || current.id !== message.conversationId) {
      return;
    }
    if (current.messages.some((row) => row.id === message.id)) {
      return;
    }
    this.detail.set({
      ...current,
      messages: [...current.messages, message],
    });
  }

  private scrollTranscriptToEnd(): void {
    const el = this.transcriptEl()?.nativeElement;
    if (!el) {
      return;
    }
    el.scrollTop = el.scrollHeight;
  }

  private isNarrow(): boolean {
    return typeof window !== "undefined" && window.matchMedia("(max-width: 720px)").matches;
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
