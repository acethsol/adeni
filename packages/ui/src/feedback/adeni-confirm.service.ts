import { Injectable, signal } from "@angular/core";

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
      confirmLabel: request.confirmLabel ?? "Confirm",
      cancelLabel: request.cancelLabel ?? "Cancel",
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
