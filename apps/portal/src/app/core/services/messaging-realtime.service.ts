import { Injectable, Injector, inject, OnDestroy } from "@angular/core";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import {
  conversationMessageSchema,
  conversationSummarySchema,
  type ConversationMessage,
  type ConversationSummary,
} from "@adeni/shared";
import { Subject } from "rxjs";
import { auth0FirstValueFrom, resolveAuthService } from "../auth0-rxjs";
import {
  ADENI_PORTAL_CONFIG,
  isAuth0Configured,
  isBusinessPortalDevMode,
} from "../adeni-config";

@Injectable({ providedIn: "root" })
export class MessagingRealtimeService implements OnDestroy {
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly injector = inject(Injector);

  private connection: HubConnection | null = null;
  private tenantId: string | null = null;
  private joinedConversationId: string | null = null;

  private readonly messageCreatedSubject = new Subject<ConversationMessage>();
  private readonly conversationUpdatedSubject = new Subject<ConversationSummary>();

  readonly messageCreated$ = this.messageCreatedSubject.asObservable();
  readonly conversationUpdated$ = this.conversationUpdatedSubject.asObservable();

  async start(tenantId: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected && this.tenantId === tenantId) {
      return;
    }

    await this.stop();
    this.tenantId = tenantId;

    const hubUrl = this.buildHubUrl();
    const builder = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.resolveAccessToken(),
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning);

    const connection = builder.build();
    connection.on("MessageCreated", (payload: unknown) => {
      const parsed = conversationMessageSchema.safeParse(payload);
      if (parsed.success) {
        this.messageCreatedSubject.next(parsed.data);
      }
    });
    connection.on("ConversationUpdated", (payload: unknown) => {
      const parsed = conversationSummarySchema.safeParse(payload);
      if (parsed.success) {
        this.conversationUpdatedSubject.next(parsed.data);
      }
    });
    connection.onreconnected(() => {
      void this.rejoinGroups();
    });

    this.connection = connection;
    await connection.start();
    await connection.invoke("JoinTenantInbox", tenantId);
  }

  async joinConversation(conversationId: string): Promise<void> {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      return;
    }

    if (this.joinedConversationId && this.joinedConversationId !== conversationId) {
      try {
        await this.connection.invoke("LeaveConversation", this.joinedConversationId);
      } catch {
        // Best-effort leave.
      }
    }

    await this.connection.invoke("JoinConversation", conversationId);
    this.joinedConversationId = conversationId;
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.tenantId = null;
    this.joinedConversationId = null;
    if (!connection) {
      return;
    }

    try {
      await connection.stop();
    } catch {
      // Ignore stop failures during teardown.
    }
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  private async rejoinGroups(): Promise<void> {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      return;
    }

    if (this.tenantId) {
      try {
        await this.connection.invoke("JoinTenantInbox", this.tenantId);
      } catch {
        // Will retry on next reconnect.
      }
    }

    if (this.joinedConversationId) {
      try {
        await this.connection.invoke("JoinConversation", this.joinedConversationId);
      } catch {
        // Will retry on next reconnect.
      }
    }
  }

  private buildHubUrl(): string {
    const base = `${this.config.apiBaseUrl}/hubs/messaging`;
    if (!isAuth0Configured(this.config) && isBusinessPortalDevMode(this.config)) {
      const sub = encodeURIComponent(this.config.devBusinessAuth0Sub);
      return `${base}?dev_auth0_sub=${sub}`;
    }

    return base;
  }

  private async resolveAccessToken(): Promise<string> {
    if (!isAuth0Configured(this.config)) {
      return "";
    }

    const auth = resolveAuthService(this.injector);
    if (!auth) {
      return "";
    }

    return auth0FirstValueFrom<string>(
      auth.getAccessTokenSilently({
        authorizationParams: {
          audience: this.config.auth0.audience,
        },
      }),
    );
  }
}
