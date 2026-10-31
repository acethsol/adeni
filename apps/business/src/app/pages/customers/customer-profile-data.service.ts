import { Injectable, inject, signal, type OnDestroy } from "@angular/core";
import type { BusinessGiftCard, CustomerActivity, CustomerProfile } from "@adeni/shared";
import { isStaffPresenceGateError } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";

/** Page-scoped read state. A new customer/branch aborts old requests and discards late responses. */
@Injectable()
export class CustomerProfileDataService implements OnDestroy {
  private readonly api = inject(BusinessApiService);
  private request: AbortController | null = null;
  private scope: { id: string; locationId?: string } | null = null;
  readonly profile = signal<CustomerProfile | null>(null);
  readonly activity = signal<CustomerActivity | null>(null);
  readonly purchasedGiftCards = signal<BusinessGiftCard[] | null>(null);
  readonly loadingGifts = signal(false);
  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly error = signal<unknown>(null);
  readonly activityPresenceBlocked = signal(false);
  readonly giftsPresenceBlocked = signal(false);

  async load(id: string, locationId?: string): Promise<void> {
    this.request?.abort();
    const request = new AbortController();
    this.request = request;
    this.scope = { id, locationId };
    this.profile.set(null);
    this.activity.set(null);
    this.purchasedGiftCards.set(null);
    this.loadingGifts.set(false);
    this.error.set(null);
    this.activityPresenceBlocked.set(false);
    this.loading.set(true);
    this.loadingMore.set(false);
    try {
      const profile = await this.api.withAuthorizedClient((client) =>
        client.getTenantCustomerProfile(id, locationId, request.signal),
      );
      if (request.signal.aborted) return;
      this.profile.set(profile);
      try {
        const activity = await this.api.withAuthorizedClient((client) =>
          client.getTenantCustomerActivity(id, { locationId }, request.signal),
        );
        if (!request.signal.aborted) {
          this.activity.set(activity);
        }
      } catch (activityError) {
        if (!request.signal.aborted && isStaffPresenceGateError(activityError)) {
          this.activityPresenceBlocked.set(true);
        } else if (!request.signal.aborted) {
          this.error.set(activityError);
        }
      }
    } catch (error) {
      if (!request.signal.aborted) {
        this.error.set(error);
      }
    } finally {
      if (this.request === request) this.loading.set(false);
    }
  }

  async loadPurchasedGiftCards(): Promise<void> {
    const request=this.request;const scope=this.scope;
    if(!request||request.signal.aborted||!scope||scope.locationId||this.profile()?.availability.finance!=="available"||this.loadingGifts())return;
    this.loadingGifts.set(true);
    try{const rows=await this.api.withAuthorizedClient(c=>c.commerce.customerCards(scope.id,request.signal));if(!request.signal.aborted)this.purchasedGiftCards.set(rows);}
    catch(e){
      if(!request.signal.aborted){
        if(isStaffPresenceGateError(e))this.giftsPresenceBlocked.set(true);
        else this.error.set(e);
      }
    }
    finally{if(this.request===request)this.loadingGifts.set(false);}
  }

  async loadMore(): Promise<void> {
    const scope = this.scope;
    const current = this.activity();
    const request = this.request;
    if (!scope || !current?.nextCursor || !request || request.signal.aborted || this.loadingMore()) return;
    this.loadingMore.set(true);
    this.error.set(null);
    try {
      const page = await this.api.withAuthorizedClient(client => client.getTenantCustomerActivity(scope.id,
        { locationId: scope.locationId, cursor: current.nextCursor ?? undefined }, request.signal));
      if (!request.signal.aborted) {
        const ids = new Set(current.items.map(item => item.id));
        this.activity.set({ ...page, items: [...current.items, ...page.items.filter(item => !ids.has(item.id))] });
      }
    } catch (error) {
      if (!request.signal.aborted) this.error.set(error);
    } finally {
      if (this.request === request) this.loadingMore.set(false);
    }
  }

  cancel(): void { this.request?.abort(); }
  ngOnDestroy(): void { this.cancel(); }
}
