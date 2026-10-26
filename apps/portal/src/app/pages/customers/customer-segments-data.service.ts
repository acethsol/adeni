import { Injectable, inject, signal } from "@angular/core";
import type { CustomerSegments } from "@adeni/shared/customer-segments";
import { BusinessApiService } from "../../core/services/business-api.service";

@Injectable()
export class CustomerSegmentsDataService {
  private readonly api = inject(BusinessApiService);
  private pending?: AbortController;
  readonly rows = signal<ReadonlyMap<string, CustomerSegments>>(new Map());
  readonly loading = signal(false);
  readonly failed = signal(false);
  cancel(): void { this.pending?.abort(); this.pending = undefined; this.rows.set(new Map()); this.loading.set(false); }
  async load(ids: string[], location?: string): Promise<void> {
    this.cancel(); if (!ids.length) return;
    const pending = this.pending = new AbortController(); this.loading.set(true); this.failed.set(false);
    try {
      const rows = await this.api.withAuthorizedClient(c => c.insights.batch(ids, location, pending.signal));
      if (this.pending === pending && !pending.signal.aborted) this.rows.set(new Map(rows.map(row => [row.tenantCustomerId, row])));
    } catch {
      if (this.pending === pending && !pending.signal.aborted) this.failed.set(true);
    } finally { if (this.pending === pending) this.loading.set(false); }
  }
}
