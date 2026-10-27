import { Injectable, signal } from "@angular/core";
import { injectAdeniTranslate } from "../locale/adeni-i18n";

export type AdeniConfirmVariant = "primary" | "danger";

/**
 * Preset action kinds with default copy under `common.confirm.<kind>.{title,message,action}`.
 * Add a kind here + EN/FR keys (overlay catalogs) rather than hard-coding strings in pages.
 */
export type AdeniConfirmKind =
  | "save"
  | "submit"
  | "send"
  | "create"
  | "activate"
  | "approve"
  | "grant"
  | "reactivate"
  | "delete"
  | "remove"
  | "deactivate"
  | "reject"
  | "deny"
  | "revoke"
  | "cancel";

/** Kinds that default to the destructive (red) variant. */
const DANGER_KINDS: ReadonlySet<AdeniConfirmKind> = new Set([
  "delete",
  "remove",
  "deactivate",
  "reject",
  "deny",
  "revoke",
  "cancel",
]);

export type AdeniConfirmRequest = {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** `danger` styles the confirm action as destructive (reject / delete / erase). Default `primary`. */
  variant?: AdeniConfirmVariant;
  /** @deprecated Use `variant: "danger"`. Kept so existing call sites keep working. */
  danger?: boolean;
  /** Shows an "I understand" checkbox; the confirm button stays disabled until it is checked. */
  requireAcknowledgement?: boolean;
  /** Overrides the default `common.confirm.understand` checkbox label. */
  acknowledgementLabel?: string;
};

export type AdeniConfirmDialog = AdeniConfirmRequest & {
  confirmLabel: string;
  cancelLabel: string;
  variant: AdeniConfirmVariant;
  danger: boolean;
  requireAcknowledgement: boolean;
  acknowledgementLabel: string;
};

export type AdeniConfirmKindOptions = {
  /** Replaces `{subject}` in the default message (e.g. a business or service name). */
  subject?: string;
  title?: string;
  message?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: AdeniConfirmVariant;
  requireAcknowledgement?: boolean;
  acknowledgementLabel?: string;
};

@Injectable({ providedIn: "root" })
export class AdeniConfirmService {
  protected readonly tr = injectAdeniTranslate();
  private readonly dialogSignal = signal<AdeniConfirmDialog | null>(null);
  private readonly acknowledgedSignal = signal(false);
  private resolver: ((value: boolean) => void) | null = null;

  readonly dialog = this.dialogSignal.asReadonly();
  /** Whether the "I understand" checkbox is currently checked (only meaningful when required). */
  readonly acknowledged = this.acknowledgedSignal.asReadonly();

  /** Resolves `true` when the user confirms, `false` on cancel / Escape / backdrop click. */
  confirm(request: AdeniConfirmRequest): Promise<boolean> {
    if (this.resolver) {
      this.resolver(false);
      this.resolver = null;
    }

    const variant: AdeniConfirmVariant = request.variant ?? (request.danger ? "danger" : "primary");

    this.acknowledgedSignal.set(false);
    this.dialogSignal.set({
      title: request.title,
      message: request.message,
      confirmLabel: request.confirmLabel ?? this.tr("common.confirm.confirmLabel"),
      cancelLabel: request.cancelLabel ?? this.tr("common.confirm.cancelLabel"),
      variant,
      danger: variant === "danger",
      requireAcknowledgement: request.requireAcknowledgement ?? false,
      acknowledgementLabel: request.acknowledgementLabel ?? this.tr("common.confirm.understand"),
    });

    return new Promise<boolean>((resolve) => {
      this.resolver = resolve;
    });
  }

  /** Confirm using preset copy for a significant action (`common.confirm.<kind>.*`). */
  confirmKind(kind: AdeniConfirmKind, options: AdeniConfirmKindOptions = {}): Promise<boolean> {
    const vars = { subject: options.subject ?? this.tr("common.confirm.thisItem") };
    return this.confirm({
      title: options.title ?? this.tr(`common.confirm.${kind}.title`),
      message: options.message ?? this.tr(`common.confirm.${kind}.message`, vars),
      confirmLabel: options.confirmLabel ?? this.tr(`common.confirm.${kind}.action`),
      cancelLabel: options.cancelLabel,
      variant: options.variant ?? (DANGER_KINDS.has(kind) ? "danger" : "primary"),
      requireAcknowledgement: options.requireAcknowledgement,
      acknowledgementLabel: options.acknowledgementLabel,
    });
  }

  setAcknowledged(value: boolean): void {
    this.acknowledgedSignal.set(value);
  }

  settle(accepted: boolean): void {
    const dialog = this.dialogSignal();
    // Guard: a confirm click is ignored until the acknowledgement checkbox is ticked.
    if (accepted && dialog?.requireAcknowledgement && !this.acknowledgedSignal()) {
      return;
    }
    const resolve = this.resolver;
    this.resolver = null;
    this.dialogSignal.set(null);
    this.acknowledgedSignal.set(false);
    resolve?.(accepted);
  }
}
