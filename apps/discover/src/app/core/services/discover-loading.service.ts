import { Injectable, computed, signal } from "@angular/core";

@Injectable({ providedIn: "root" })
export class DiscoverLoadingService {
  private readonly depth = signal(0);
  private readonly labelSignal = signal("Loading…");
  private readonly detailSignal = signal("Just a moment");

  readonly visible = computed(() => this.depth() > 0);
  readonly label = this.labelSignal.asReadonly();
  readonly detail = this.detailSignal.asReadonly();

  show(label = "Loading…", detail = "Just a moment"): void {
    this.labelSignal.set(label);
    this.detailSignal.set(detail);
    this.depth.update((n) => n + 1);
  }

  hide(): void {
    this.depth.update((n) => Math.max(0, n - 1));
  }

  async run<T>(work: () => Promise<T>, label?: string, detail?: string): Promise<T> {
    this.show(label, detail);
    try {
      return await work();
    } finally {
      this.hide();
    }
  }
}
