import { Injectable, computed, inject, signal } from "@angular/core";
import { defaultLocale, t } from "@adeni/shared";
import { AdeniLocaleService } from "../locale/adeni-locale.service";

export type AdeniToastTone = "success" | "error" | "info";

export type AdeniToast = {
  id: number;
  tone: AdeniToastTone;
  title: string;
  message: string;
};

@Injectable({ providedIn: "root" })
export class AdeniFeedbackService {
  /** Optional so unit tests / hosts without `provideAdeniLocale` fall back to English. */
  private readonly localeService = inject(AdeniLocaleService, { optional: true });
  private readonly loadDepth = signal(0);
  /** `null` = use the localized default (`toast.loading` / `toast.loadingDetail`). */
  private readonly labelSignal = signal<string | null>(null);
  private readonly detailSignal = signal<string | null>(null);
  private readonly toastsSignal = signal<AdeniToast[]>([]);
  private toastSeq = 0;
  private readonly dismissTimers = new Map<number, ReturnType<typeof setTimeout>>();

  readonly loadingVisible = computed(() => this.loadDepth() > 0);
  readonly loadingLabel = computed(() => this.labelSignal() ?? this.text("toast.loading"));
  readonly loadingDetail = computed(() => this.detailSignal() ?? this.text("toast.loadingDetail"));
  readonly toasts = this.toastsSignal.asReadonly();

  /** Localized string in the active locale; reactive when read inside `computed`/templates. */
  text(key: string, vars?: Record<string, string | number>): string {
    return t(this.localeService?.locale() ?? defaultLocale, key, vars);
  }

  showLoading(label?: string, detail?: string): void {
    this.labelSignal.set(label ?? null);
    this.detailSignal.set(detail ?? null);
    this.loadDepth.update((n) => n + 1);
  }

  hideLoading(): void {
    this.loadDepth.update((n) => Math.max(0, n - 1));
  }

  async runLoading<T>(
    work: () => Promise<T>,
    label?: string,
    detail?: string,
  ): Promise<T> {
    this.showLoading(label, detail);
    try {
      return await work();
    } finally {
      this.hideLoading();
    }
  }

  success(message: string, title?: string): void {
    this.push("success", title ?? this.text("toast.saved"), message);
  }

  error(message: string, title?: string): void {
    this.push("error", title ?? this.text("toast.somethingWrong"), message);
  }

  info(message: string, title?: string): void {
    this.push("info", title ?? this.text("toast.notice"), message);
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
