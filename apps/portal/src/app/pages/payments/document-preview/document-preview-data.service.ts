import { Injectable, inject, signal } from "@angular/core";
import { BusinessApiService } from "../../../core/services/business-api.service";

@Injectable()
export class DocumentPreviewDataService {
  private readonly api = inject(BusinessApiService);
  private pending?: AbortController;
  readonly url = signal<string | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  clear(): void {
    this.pending?.abort(); this.pending = undefined;
    const previous = this.url(); if (previous) URL.revokeObjectURL(previous);
    this.url.set(null); this.loading.set(false);
  }
  async load(id: string, location?: string): Promise<void> {
    this.clear(); const pending = this.pending = new AbortController(); this.loading.set(true); this.failed.set(false);
    try {
      const blob = await this.api.withAuthorizedClient(c => c.insights.preview(id, location, pending.signal));
      if (this.pending !== pending || pending.signal.aborted) return;
      // Validate again at the view boundary; only locally generated PDF Blob URLs enter the viewer.
      if (blob.type !== "application/pdf" || blob.size > 5 * 1024 * 1024) throw new Error("Invalid private PDF.");
      this.url.set(URL.createObjectURL(blob));
    } catch {
      if (this.pending === pending && !pending.signal.aborted) this.failed.set(true);
    } finally { if (this.pending === pending) this.loading.set(false); }
  }
}
