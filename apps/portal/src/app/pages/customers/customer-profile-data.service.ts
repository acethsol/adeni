import { Injectable, inject, signal, type OnDestroy } from "@angular/core";
import type { CustomerActivity, CustomerProfile } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";

/** Page-scoped read state. A new customer/branch aborts old requests and discards late responses. */
@Injectable()
export class CustomerProfileDataService implements OnDestroy {
  private readonly api = inject(BusinessApiService);
  private request: AbortController | null = null;
  private scope: { id: string; locationId?: string } | null = null;
  readonly profile = signal<CustomerProfile | null>(null);
  readonly activity = signal<CustomerActivity | null>(null);
  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly error = signal<unknown>(null);

  async load(id: string, locationId?: string): Promise<void> {
    this.request?.abort();
    const request = new AbortController();
    this.request = request;
    this.scope = { id, locationId };
    this.profile.set(null);
    this.activity.set(null);
    this.error.set(null);
    this.loading.set(true);
    this.loadingMore.set(false);
    try {
      const [profile, activity] = await this.api.withAuthorizedClient(client => Promise.all([
        client.getTenantCustomerProfile(id, locationId, request.signal),
        client.getTenantCustomerActivity(id, { locationId }, request.signal),
      ]));
      if (request.signal.aborted) return;
      this.profile.set(profile);
      this.activity.set(activity);
    } catch (error) {
      if (!request.signal.aborted) {
        this.error.set(error);
        // Cancel the sibling request if one of the two endpoints fails.
        request.abort();
      }
    } finally {
      if (this.request === request) this.loading.set(false);
    }
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
