import { Injectable, inject, OnDestroy } from "@angular/core";
import {
  conversationMessageSchema,
  conversationSummarySchema,
  readUpdatedRealtimeSchema,
  type ConversationMessage,
  type ConversationSummary,
  type ReadUpdatedRealtimeEvent,
} from "@adeni/shared";
import { Subject } from "rxjs";
import { ADENI_REALTIME_AUTH } from "./adeni-realtime-auth.token";
import { SignalRHubSession } from "./signalr-hub-session";

@Injectable({ providedIn: "root" })
export class AdeniMessagingRealtimeService implements OnDestroy {
  private readonly auth = inject(ADENI_REALTIME_AUTH);
  private readonly session = new SignalRHubSession();

  private tenantId: string | null = null;
  private joinedConversationId: string | null = null;
  private customerMode = false;

  private readonly messageCreatedSubject = new Subject<ConversationMessage>();
  private readonly conversationUpdatedSubject = new Subject<ConversationSummary>();
  private readonly readUpdatedSubject = new Subject<ReadUpdatedRealtimeEvent>();

  readonly messageCreated$ = this.messageCreatedSubject.asObservable();
  readonly conversationUpdated$ = this.conversationUpdatedSubject.asObservable();
  readonly readUpdated$ = this.readUpdatedSubject.asObservable();

  isConnected(): boolean {
    return this.session.isConnected();
  }

  /** Business portal: tenant inbox + conversation events. */
  async start(tenantId: string): Promise<void> {
    return this.startTenantInbox(tenantId);
  }

  async startTenantInbox(tenantId: string): Promise<void> {
    if (this.session.isConnected() && this.tenantId === tenantId && !this.customerMode) {
      return;
    }

    await this.stop();
    this.tenantId = tenantId;
    this.customerMode = false;

    await this.session.connect({
      hubUrl: this.auth.getHubUrl("messaging"),
      accessTokenFactory: () => this.auth.getAccessToken(),
      handlers: this.messagingHandlers(true),
      onReconnected: () => this.rejoinGroups(),
    });
    await this.session.invoke("JoinTenantInbox", tenantId);
  }

  /** Discover consumer: connect for per-conversation groups only. */
  async ensureCustomerConnected(): Promise<void> {
    if (this.session.isConnected() && this.customerMode) {
      return;
    }

    await this.stop();
    this.customerMode = true;

    await this.session.connect({
      hubUrl: this.auth.getHubUrl("messaging"),
      accessTokenFactory: () => this.auth.getAccessToken(),
      handlers: this.messagingHandlers(false),
      onReconnected: () => this.rejoinConversation(),
    });
  }

  async joinConversation(conversationId: string): Promise<void> {
    if (this.customerMode) {
      await this.ensureCustomerConnected();
    }

    if (!this.session.isConnected()) {
      return;
    }

    if (this.joinedConversationId && this.joinedConversationId !== conversationId) {
      try {
        await this.session.invoke("LeaveConversation", this.joinedConversationId);
      } catch {
        // best-effort
      }
    }

    await this.session.invoke("JoinConversation", conversationId);
    this.joinedConversationId = conversationId;
  }

  async stop(): Promise<void> {
    this.tenantId = null;
    this.joinedConversationId = null;
    this.customerMode = false;
    await this.session.disconnect();
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  private messagingHandlers(includeInboxEvents: boolean): Record<string, (payload: unknown) => void> {
    const handlers: Record<string, (payload: unknown) => void> = {
      MessageCreated: (payload) => {
        const parsed = conversationMessageSchema.safeParse(payload);
        if (parsed.success) {
          this.messageCreatedSubject.next(parsed.data);
        }
      },
    };

    if (includeInboxEvents) {
      handlers["ConversationUpdated"] = (payload) => {
        const parsed = conversationSummarySchema.safeParse(payload);
        if (parsed.success) {
          this.conversationUpdatedSubject.next(parsed.data);
        }
      };
      handlers["ReadUpdated"] = (payload) => {
        const parsed = readUpdatedRealtimeSchema.safeParse(payload);
        if (parsed.success) {
          this.readUpdatedSubject.next(parsed.data);
        }
      };
    }

    return handlers;
  }

  private async rejoinGroups(): Promise<void> {
    if (!this.session.isConnected()) {
      return;
    }
    if (this.tenantId) {
      try {
        await this.session.invoke("JoinTenantInbox", this.tenantId);
      } catch {
        // retry on reconnect
      }
    }
    await this.rejoinConversation();
  }

  private async rejoinConversation(): Promise<void> {
    if (!this.joinedConversationId) {
      return;
    }
    try {
      await this.joinConversation(this.joinedConversationId);
    } catch {
      // retry on reconnect
    }
  }
}
