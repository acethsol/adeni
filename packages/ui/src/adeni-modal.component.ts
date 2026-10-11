import { Component, HostListener, input, output } from "@angular/core";

export type AdeniModalSize = "md" | "lg" | "xl";

/**
 * Shared modal shell for portal + admin forms (create/edit wizards, confirm-style dialogs).
 * Use instead of inline page sections for create/edit flows.
 */
@Component({
  selector: "adeni-modal",
  standalone: true,
  template: `
    @if (open()) {
      <div class="backdrop" role="presentation" (click)="onBackdrop()">
        <div
          class="panel"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
          [attr.data-size]="size()"
          (click)="$event.stopPropagation()"
        >
          <header class="header">
            <h2 [id]="titleId" class="title">{{ title() }}</h2>
            @if (subtitle()) {
              <p class="subtitle">{{ subtitle() }}</p>
            }
            <button
              type="button"
              class="close"
              aria-label="Close"
              (click)="closed.emit()"
            >
              ×
            </button>
          </header>
          <div class="body">
            <ng-content />
          </div>
          @if (hasFooter()) {
            <footer class="footer">
              <ng-content select="[modalFooter]" />
            </footer>
          }
        </div>
      </div>
    }
  `,
  styles: `
    .backdrop {
      position: fixed;
      inset: 0;
      z-index: 1200;
      display: grid;
      place-items: center;
      padding: 1.25rem;
      background: rgb(22 22 22 / 48%);
      animation: fade-in 140ms ease;
    }

    .panel {
      display: flex;
      flex-direction: column;
      width: min(32rem, 100%);
      max-height: min(90vh, 52rem);
      background: var(--cds-layer-01, #fff);
      color: var(--cds-text-primary, #161616);
      border: 1px solid var(--cds-border-subtle-01, #e0e0e0);
      box-shadow: 0 16px 40px rgb(0 0 0 / 22%);
      animation: rise-in 160ms ease;
    }

    .panel[data-size="lg"] {
      width: min(40rem, 100%);
    }

    .panel[data-size="xl"] {
      width: min(48rem, 100%);
    }

    :host-context([data-theme="dark"]) .panel {
      background: #262626;
      border-color: #393939;
      color: #f4f4f4;
    }

    .header {
      position: relative;
      padding: 1.15rem 2.75rem 0.85rem 1.25rem;
      border-bottom: 1px solid var(--cds-border-subtle-01, #e0e0e0);
      flex: 0 0 auto;
    }

    :host-context([data-theme="dark"]) .header {
      border-bottom-color: #393939;
    }

    .title {
      margin: 0;
      font-size: 1.15rem;
      font-weight: 650;
      letter-spacing: -0.015em;
      line-height: 1.3;
    }

    .subtitle {
      margin: 0.35rem 0 0;
      font-size: 0.875rem;
      line-height: 1.4;
      color: var(--cds-text-secondary, #525252);
    }

    :host-context([data-theme="dark"]) .subtitle {
      color: #c6c6c6;
    }

    .close {
      position: absolute;
      top: 0.75rem;
      right: 0.65rem;
      width: 2.25rem;
      height: 2.25rem;
      border: 0;
      background: transparent;
      color: inherit;
      font-size: 1.5rem;
      line-height: 1;
      cursor: pointer;
      opacity: 0.7;
    }

    .close:hover {
      opacity: 1;
    }

    .body {
      padding: 1.15rem 1.25rem;
      overflow: auto;
      flex: 1 1 auto;
    }

    .footer {
      display: flex;
      justify-content: flex-end;
      flex-wrap: wrap;
      gap: 0.5rem;
      padding: 0.85rem 1.25rem 1.1rem;
      border-top: 1px solid var(--cds-border-subtle-01, #e0e0e0);
      flex: 0 0 auto;
    }

    :host-context([data-theme="dark"]) .footer {
      border-top-color: #393939;
    }

    @keyframes fade-in {
      from {
        opacity: 0;
      }
      to {
        opacity: 1;
      }
    }

    @keyframes rise-in {
      from {
        opacity: 0;
        transform: translateY(0.4rem);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }
  `,
})
export class AdeniModalComponent {
  readonly open = input(false);
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
  readonly size = input<AdeniModalSize>("md");
  /** When true, renders the modalFooter projection slot. */
  readonly hasFooter = input(true);
  readonly closeOnBackdrop = input(true);
  readonly closed = output<void>();

  readonly titleId = `adeni-modal-title-${Math.random().toString(36).slice(2, 9)}`;

  @HostListener("document:keydown.escape")
  onEscape(): void {
    if (this.open()) {
      this.closed.emit();
    }
  }

  onBackdrop(): void {
    if (this.closeOnBackdrop()) {
      this.closed.emit();
    }
  }
}
