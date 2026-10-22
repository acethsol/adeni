import { Injectable, signal } from "@angular/core";
import { injectAdeniTranslate } from "../locale/adeni-i18n";

export type AdeniConfirmRequest = {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** Styles the confirm action as destructive (reject / delete / erase). */
  danger?: boolean;
};

export type AdeniConfirmDialog = AdeniConfirmRequest & {
  confirmLabel: string;
  cancelLabel: string;
  danger: boolean;
};

@Injectable({ providedIn: "root" })
export class AdeniConfirmService {
  protected readonly tr = injectAdeniTranslate();
  private readonly dialogSignal = signal<AdeniConfirmDialog | null>(null);
  private resolver: ((value: boolean) => void) | null = null;

  readonly dialog = this.dialogSignal.asReadonly();

  confirm(request: AdeniConfirmRequest): Promise<boolean> {
    if (this.resolver) {
      this.resolver(false);
      this.resolver = null;
    }

    this.dialogSignal.set({
      title: request.title,
      message: request.message,
      confirmLabel: request.confirmLabel ?? this.tr("ui.ui.confirm"),
      cancelLabel: request.cancelLabel ?? this.tr("common.cancel"),
      danger: request.danger ?? false,
    });

    return new Promise<boolean>((resolve) => {
      this.resolver = resolve;
    });
  }

  settle(accepted: boolean): void {
    const resolve = this.resolver;
    this.resolver = null;
    this.dialogSignal.set(null);
    resolve?.(accepted);
  }
}
