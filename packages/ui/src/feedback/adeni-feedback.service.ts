/**
 * Ref-counted global loading overlay + toast queue (`AdeniGlobalLoadingPanelComponent` / toast host in each app shell).
 * Only code paths that call `showLoading` / `runLoading` show the panel — not automatic for every async delay or page `loading` signals.
 * Default copy: `toast.loading` + `toast.loadingDetail` in `@adeni/shared`; pass label/detail only when the action needs specific messaging.
 */
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

type LoadingFrame = {
  label: string | null;
  detail: string | null;
};

@Injectable({ providedIn: "root" })
export class AdeniFeedbackService {
  /** Optional so unit tests / hosts without `provideAdeniLocale` fall back to English. */
  private readonly localeService = inject(AdeniLocaleService, { optional: true });
  /** Nested `showLoading` / `hideLoading` pairs — top frame drives visible copy. */
  private readonly loadingFrames = signal<LoadingFrame[]>([]);
  private readonly toastsSignal = signal<AdeniToast[]>([]);
  private toastSeq = 0;
  private readonly dismissTimers = new Map<number, ReturnType<typeof setTimeout>>();

  readonly loadingVisible = computed(() => this.loadingFrames().length > 0);
  /** Nested `showLoading` count — used for subtle depth cue in the global panel. */
  readonly loadingDepth = computed(() => this.loadingFrames().length);
  readonly loadingLabel = computed(() => {
    const top = this.topLoadingFrame();
    return top?.label ?? this.text("toast.loading");
  });
  readonly loadingDetail = computed(() => {
    const top = this.topLoadingFrame();
    return top?.detail ?? this.text("toast.loadingDetail");
  });
  readonly toasts = this.toastsSignal.asReadonly();

  /** Localized string in the active locale; reactive when read inside `computed`/templates. */
  text(key: string, vars?: Record<string, string | number>): string {
    return t(this.localeService?.locale() ?? defaultLocale, key, vars);
  }

  showLoading(label?: string, detail?: string): void {
    this.loadingFrames.update((frames) => [
      ...frames,
      { label: label ?? null, detail: detail ?? null },
    ]);
  }

  hideLoading(): void {
    this.loadingFrames.update((frames) => {
      if (frames.length === 0) {
        return frames;
      }
      return frames.slice(0, -1);
    });
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

  private topLoadingFrame(): LoadingFrame | undefined {
    const frames = this.loadingFrames();
    return frames.length > 0 ? frames[frames.length - 1] : undefined;
  }

  private push(tone: AdeniToastTone, title: string, message: string): void {
    const id = ++this.toastSeq;
    this.toastsSignal.update((list) => [...list, { id, tone, title, message }].slice(-5));
    const timer = setTimeout(() => this.dismiss(id), tone === "error" ? 7000 : 4200);
    this.dismissTimers.set(id, timer);
  }
}
