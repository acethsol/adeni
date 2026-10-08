import { Injectable, computed, signal } from "@angular/core";

export type AdeniToastTone = "success" | "error" | "info";

export type AdeniToast = {
  id: number;
  tone: AdeniToastTone;
  title: string;
  message: string;
};

@Injectable({ providedIn: "root" })
export class AdeniFeedbackService {
  private readonly loadDepth = signal(0);
  private readonly labelSignal = signal("Loading…");
  private readonly detailSignal = signal("Just a moment");
  private readonly toastsSignal = signal<AdeniToast[]>([]);
  private toastSeq = 0;
  private readonly dismissTimers = new Map<number, ReturnType<typeof setTimeout>>();

  readonly loadingVisible = computed(() => this.loadDepth() > 0);
  readonly loadingLabel = this.labelSignal.asReadonly();
  readonly loadingDetail = this.detailSignal.asReadonly();
  readonly toasts = this.toastsSignal.asReadonly();

  showLoading(label = "Loading…", detail = "Just a moment"): void {
    this.labelSignal.set(label);
    this.detailSignal.set(detail);
    this.loadDepth.update((n) => n + 1);
  }

  hideLoading(): void {
    this.loadDepth.update((n) => Math.max(0, n - 1));
  }

  async runLoading<T>(
    work: () => Promise<T>,
    label = "Loading…",
    detail = "Just a moment",
  ): Promise<T> {
    this.showLoading(label, detail);
    try {
      return await work();
    } finally {
      this.hideLoading();
    }
  }

  success(message: string, title = "Saved"): void {
    this.push("success", title, message);
  }

  error(message: string, title = "Something went wrong"): void {
    this.push("error", title, message);
  }

  info(message: string, title = "Notice"): void {
    this.push("info", title, message);
  }

  dismiss(id: number): void {
    const timer = this.dismissTimers.get(id);
    if (timer) {
      clearTimeout(timer);
      this.dismissTimers.delete(id);
    }
    this.toastsSignal.update((list) => list.filter((t) => t.id !== id));
  }

  private push(tone: AdeniToastTone, title: string, message: string): void {
    const id = ++this.toastSeq;
    this.toastsSignal.update((list) => [...list, { id, tone, title, message }].slice(-5));
    const timer = setTimeout(() => this.dismiss(id), tone === "error" ? 7000 : 4200);
    this.dismissTimers.set(id, timer);
  }
}
