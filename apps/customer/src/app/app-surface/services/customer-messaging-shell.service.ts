import { DestroyRef, inject, Injectable } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { merge } from "rxjs";
import { AdeniMessagingRealtimeService } from "@adeni/ui";
import { CustomerMessagingBadgeService } from "./customer-messaging-badge.service";

/** Shell-scoped messaging hub: live badge refresh while the consumer app is open. */
@Injectable({ providedIn: "root" })
export class CustomerMessagingShellService {
  private readonly realtime = inject(AdeniMessagingRealtimeService);
  private readonly badge = inject(CustomerMessagingBadgeService);
  private readonly destroyRef = inject(DestroyRef);

  start(): void {
    void this.realtime.ensureCustomerConnected();
    merge(this.realtime.messageCreated$, this.realtime.conversationUpdated$)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        void this.badge.refresh(true);
      });
  }
}
