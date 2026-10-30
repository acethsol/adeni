import { inject, Injectable, signal } from "@angular/core";
import type { ConversationSummary } from "@adeni/shared";
import { CustomerApiService } from "../../core/services/customer-api.service";

const MIN_REFRESH_INTERVAL_MS = 15_000;

/**
 * Unread-conversation count for the shell Messages badge.
 *
 * Phase A: pull-based (`needsAttention` from the conversation list), refreshed by the shell on navigation.
 * Deferred: lift SignalR out of `MyMessagesComponent` into a shell-scoped owner for live badge updates
 * (needs a per-customer hub group first — C1 risk R6).
 */
@Injectable({ providedIn: "root" })
export class CustomerMessagingBadgeService {
  private readonly api = inject(CustomerApiService);
  private lastRefreshAt = 0;
  private inFlight: Promise<void> | null = null;

  readonly unread = signal(0);

  /** Called by the conversation page after it loads or marks threads read. */
  setFromConversations(rows: readonly Pick<ConversationSummary, "needsAttention">[]): void {
    this.unread.set(rows.filter((row) => row.needsAttention).length);
    this.lastRefreshAt = Date.now();
  }

  clear(): void {
    this.unread.set(0);
    this.lastRefreshAt = 0;
  }

  /** Throttled unless `force`; failures leave the previous count (a badge must never break the shell). */
  refresh(force = false): Promise<void> {
    if (this.inFlight) {
      return this.inFlight;
    }
    if (!force && Date.now() - this.lastRefreshAt < MIN_REFRESH_INTERVAL_MS) {
      return Promise.resolve();
    }

    this.inFlight = this.api
      .withAuthorizedClient((client) => client.listCustomerConversations())
      .then((rows) => this.setFromConversations(rows))
      .catch(() => undefined)
      .finally(() => {
        this.inFlight = null;
      });
    return this.inFlight;
  }
}
