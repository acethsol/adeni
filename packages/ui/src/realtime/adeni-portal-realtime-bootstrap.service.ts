import { Injectable, inject } from "@angular/core";
import { hasTenantEntitlement, type BusinessProfile } from "@adeni/shared";
import { AdeniMessagingRealtimeService } from "./adeni-messaging-realtime.service";
import { AdeniOperationsRealtimeService } from "./adeni-operations-realtime.service";

/** Starts tenant SignalR hubs once per portal session when profile is available. */
@Injectable({ providedIn: "root" })
export class AdeniPortalRealtimeBootstrapService {
  private readonly messaging = inject(AdeniMessagingRealtimeService);
  private readonly operations = inject(AdeniOperationsRealtimeService);
  private startedForTenant: string | null = null;
  private syncChain: Promise<void> = Promise.resolve();

  syncForProfile(profile: BusinessProfile | null): Promise<void> {
    this.syncChain = this.syncChain.then(() => this.syncForProfileInner(profile));
    return this.syncChain;
  }

  private async syncForProfileInner(profile: BusinessProfile | null): Promise<void> {
    if (!profile?.tenantId) {
      await this.stop();
      return;
    }

    const wantsMessaging = hasTenantEntitlement(profile.entitlements, "messaging");
    if (
      this.startedForTenant === profile.tenantId
      && this.operations.isConnected()
      && (!wantsMessaging || this.messaging.isConnected())
    ) {
      return;
    }

    await this.stop();

    const tasks: Promise<void>[] = [this.operations.startTenant(profile.tenantId)];
    if (wantsMessaging) {
      tasks.push(this.messaging.startTenantInbox(profile.tenantId));
    }

    const results = await Promise.allSettled(tasks);
    const opsOk = results[0]?.status === "fulfilled";
    const msgOk = !wantsMessaging || results[1]?.status === "fulfilled";
    if (opsOk && msgOk) {
      this.startedForTenant = profile.tenantId;
    }
  }

  async stop(): Promise<void> {
    this.startedForTenant = null;
    await Promise.allSettled([this.messaging.stop(), this.operations.stop()]);
  }
}
