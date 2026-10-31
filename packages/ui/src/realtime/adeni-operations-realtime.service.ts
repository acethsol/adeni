import { Injectable, inject, OnDestroy } from "@angular/core";
import {
  adminBadgesChangedSchema,
  adminVerificationChangedSchema,
  paymentCompletedRealtimeSchema,
  pendingBookingsChangedSchema,
  queueFloorChangedSchema,
  quotePipelineChangedSchema,
  tenantBadgesChangedSchema,
  tenantVerificationChangedSchema,
  attendanceTeamChangedSchema,
  type AdminBadgesChangedEvent,
  type AttendanceTeamChangedEvent,
  type AdminVerificationChangedEvent,
  type PaymentCompletedRealtimeEvent,
  type PendingBookingsChangedEvent,
  type QueueFloorChangedEvent,
  type QuotePipelineChangedEvent,
  type TenantBadgesChangedEvent,
  type TenantVerificationChangedEvent,
} from "@adeni/shared";
import { Subject } from "rxjs";
import { ADENI_REALTIME_AUTH } from "./adeni-realtime-auth.token";
import { SignalRHubSession } from "./signalr-hub-session";

type OperationsMode = "tenant" | "admin" | "checkout";

@Injectable({ providedIn: "root" })
export class AdeniOperationsRealtimeService implements OnDestroy {
  private readonly auth = inject(ADENI_REALTIME_AUTH);
  private readonly session = new SignalRHubSession();

  private mode: OperationsMode | null = null;
  private tenantId: string | null = null;
  private checkoutRealtimeToken: string | null = null;

  private readonly pendingBookingsSubject = new Subject<PendingBookingsChangedEvent>();
  private readonly queueFloorSubject = new Subject<QueueFloorChangedEvent>();
  private readonly quotePipelineSubject = new Subject<QuotePipelineChangedEvent>();
  private readonly paymentCompletedSubject = new Subject<PaymentCompletedRealtimeEvent>();
  private readonly adminVerificationSubject = new Subject<AdminVerificationChangedEvent>();
  private readonly adminBadgesSubject = new Subject<AdminBadgesChangedEvent>();
  private readonly tenantVerificationSubject = new Subject<TenantVerificationChangedEvent>();
  private readonly tenantBadgesSubject = new Subject<TenantBadgesChangedEvent>();
  private readonly attendanceTeamSubject = new Subject<AttendanceTeamChangedEvent>();

  readonly pendingBookingsChanged$ = this.pendingBookingsSubject.asObservable();
  readonly queueFloorChanged$ = this.queueFloorSubject.asObservable();
  readonly quotePipelineChanged$ = this.quotePipelineSubject.asObservable();
  readonly paymentCompleted$ = this.paymentCompletedSubject.asObservable();
  readonly adminVerificationChanged$ = this.adminVerificationSubject.asObservable();
  readonly adminBadgesChanged$ = this.adminBadgesSubject.asObservable();
  readonly tenantVerificationChanged$ = this.tenantVerificationSubject.asObservable();
  readonly tenantBadgesChanged$ = this.tenantBadgesSubject.asObservable();
  readonly attendanceTeamChanged$ = this.attendanceTeamSubject.asObservable();

  isConnected(): boolean {
    return this.session.isConnected();
  }

  /** Portal: pass tenant id. Admin: omit to join verification queue. */
  async start(tenantId?: string): Promise<void> {
    if (tenantId) {
      return this.startTenant(tenantId);
    }
    return this.startAdminVerification();
  }

  async startTenant(tenantId: string): Promise<void> {
    if (this.session.isConnected() && this.mode === "tenant" && this.tenantId === tenantId) {
      return;
    }

    await this.stopInternal();
    this.mode = "tenant";
    this.tenantId = tenantId;

    await this.session.connect({
      hubUrl: this.auth.getHubUrl("operations"),
      accessTokenFactory: () => this.auth.getAccessToken(),
      handlers: this.tenantHandlers(),
      onReconnected: () => this.rejoinTenant(),
    });
    await this.session.invoke("JoinTenant", tenantId);
  }

  async startAdminVerification(): Promise<void> {
    if (this.session.isConnected() && this.mode === "admin") {
      return;
    }

    await this.stopInternal();
    this.mode = "admin";

    await this.session.connect({
      hubUrl: this.auth.getHubUrl("operations"),
      accessTokenFactory: () => this.auth.getAccessToken(),
      handlers: {
        AdminVerificationChanged: (payload) => {
          const parsed = adminVerificationChangedSchema.safeParse(payload);
          if (parsed.success) {
            this.adminVerificationSubject.next(parsed.data);
          }
        },
        AdminBadgesChanged: (payload) => {
          const parsed = adminBadgesChangedSchema.safeParse(payload);
          if (parsed.success) {
            this.adminBadgesSubject.next(parsed.data);
          }
        },
      },
      onReconnected: () => this.session.invoke("JoinAdminVerification"),
    });
    await this.session.invoke("JoinAdminVerification");
  }

  /** Public checkout: anonymous hub join via signed token from payment initialize. */
  async watchCheckout(checkoutRealtimeToken: string): Promise<void> {
    return this.watchPaymentCheckout(checkoutRealtimeToken);
  }

  async watchPaymentCheckout(checkoutRealtimeToken: string): Promise<void> {
    const token = checkoutRealtimeToken.trim();
    if (!token) {
      return;
    }

    if (this.session.isConnected() && this.mode === "checkout" && this.checkoutRealtimeToken === token) {
      return;
    }

    await this.stopInternal();
    this.mode = "checkout";
    this.checkoutRealtimeToken = token;

    await this.session.connect({
      hubUrl: this.auth.getHubUrl("operations"),
      handlers: {
        PaymentCompleted: (payload) => {
          const parsed = paymentCompletedRealtimeSchema.safeParse(payload);
          if (parsed.success) {
            this.paymentCompletedSubject.next(parsed.data);
          }
        },
      },
      onReconnected: () => this.session.invoke("JoinPaymentCheckout", token),
    });
    await this.session.invoke("JoinPaymentCheckout", token);
  }

  async stop(): Promise<void> {
    await this.stopInternal();
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  private tenantHandlers(): Record<string, (payload: unknown) => void> {
    return {
      PendingBookingsChanged: (payload) => {
        const parsed = pendingBookingsChangedSchema.safeParse(payload);
        if (parsed.success) {
          this.pendingBookingsSubject.next(parsed.data);
        }
      },
      QueueFloorChanged: (payload) => {
        const parsed = queueFloorChangedSchema.safeParse(payload);
        if (parsed.success) {
          this.queueFloorSubject.next(parsed.data);
        }
      },
      QuotePipelineChanged: (payload) => {
        const parsed = quotePipelineChangedSchema.safeParse(payload);
        if (parsed.success) {
          this.quotePipelineSubject.next(parsed.data);
        }
      },
      PaymentCompleted: (payload) => {
        const parsed = paymentCompletedRealtimeSchema.safeParse(payload);
        if (parsed.success) {
          this.paymentCompletedSubject.next(parsed.data);
        }
      },
      TenantVerificationChanged: (payload) => {
        const parsed = tenantVerificationChangedSchema.safeParse(payload);
        if (parsed.success) {
          this.tenantVerificationSubject.next(parsed.data);
        }
      },
      TenantBadgesChanged: (payload) => {
        const parsed = tenantBadgesChangedSchema.safeParse(payload);
        if (parsed.success) {
          this.tenantBadgesSubject.next(parsed.data);
        }
      },
      AttendanceTeamChanged: (payload) => {
        const parsed = attendanceTeamChangedSchema.safeParse(payload);
        if (parsed.success) {
          this.attendanceTeamSubject.next(parsed.data);
        }
      },
    };
  }

  private async rejoinTenant(): Promise<void> {
    if (this.tenantId) {
      try {
        await this.session.invoke("JoinTenant", this.tenantId);
      } catch {
        // retry on reconnect
      }
    }
  }

  private async stopInternal(): Promise<void> {
    const token = this.checkoutRealtimeToken;
    this.mode = null;
    this.tenantId = null;
    this.checkoutRealtimeToken = null;

    if (token && this.session.isConnected()) {
      try {
        await this.session.invoke("LeavePaymentCheckout", token);
      } catch {
        // ignore
      }
    }

    await this.session.disconnect();
  }
}
