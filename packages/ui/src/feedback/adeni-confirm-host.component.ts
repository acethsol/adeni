import { Component, effect, ElementRef, inject, viewChild } from "@angular/core";
import { AdeniConfirmService } from "./adeni-confirm.service";

@Component({
  selector: "adeni-confirm-host",
  standalone: true,
  template: `
    @if (confirm.dialog(); as dialog) {
      <div
        class="backdrop"
        role="presentation"
        (click)="confirm.settle(false)"
      >
        <div
          #panel
          class="dialog"
          role="alertdialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
          [attr.aria-describedby]="bodyId"
          (click)="$event.stopPropagation()"
          (keydown)="onKeydown($event)"
        >
          <h2 [id]="titleId">{{ dialog.title }}</h2>
          <p [id]="bodyId">{{ dialog.message }}</p>
          <div class="actions">
            <button type="button" class="btn ghost" (click)="confirm.settle(false)">
              {{ dialog.cancelLabel }}
            </button>
            <button
              type="button"
              class="btn"
              [class.danger]="dialog.danger"
              [class.primary]="!dialog.danger"
              (click)="confirm.settle(true)"
            >
              {{ dialog.confirmLabel }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: `
    .backdrop {
      position: fixed;
      inset: 0;
      z-index: 1400;
      display: grid;
      place-items: center;
      padding: 1.25rem;
      background: rgb(22 22 22 / 45%);
      animation: fade-in 140ms ease;
    }

    .dialog {
      width: min(24rem, 100%);
      padding: 1.25rem 1.25rem 1.1rem;
      border: 1px solid var(--cds-border-subtle, #e0e0e0);
      border-radius: var(--cds-radius-none, 0);
      background: var(--cds-layer-01, #fff);
      box-shadow: 0 12px 32px rgb(0 0 0 / 22%);
      animation: rise-in 160ms ease;
    }

    :host-context([data-theme="dark"]) .dialog {
      background: #262626;
      border-color: #393939;
      color: #f4f4f4;
    }

    h2 {
      margin: 0;
      font-size: 1.05rem;
      font-weight: 700;
      letter-spacing: -0.01em;
    }

    p {
      margin: 0.55rem 0 0;
      font-size: 0.9rem;
      line-height: 1.45;
      color: var(--cds-text-secondary, #525252);
    }

    :host-context([data-theme="dark"]) p {
      color: #c6c6c6;
    }

    .actions {
      display: flex;
      justify-content: flex-end;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin-top: 1.15rem;
    }

    .btn {
      min-height: 2.5rem;
      padding: 0 1rem;
      border: 1px solid transparent;
      border-radius: var(--cds-radius-none, 0);
      font: inherit;
      font-size: 0.875rem;
      font-weight: 600;
      cursor: pointer;
    }

    .btn.ghost {
      background: transparent;
      border-color: var(--cds-border-strong, #8d8d8d);
      color: inherit;
    }

    .btn.ghost:hover {
      background: rgb(0 0 0 / 4%);
    }

    .btn.primary {
      background: var(--interactive, #7f56ff);
      color: #fff;
    }

    .btn.primary:hover {
      background: var(--interactive-hover, #6f42ff);
    }

    .btn.danger {
      background: #da1e28;
      color: #fff;
    }

    .btn.danger:hover {
      background: #b81922;
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
        transform: translateY(8px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .backdrop,
      .dialog {
        animation: none;
      }
    }
  `,
})
export class AdeniConfirmHostComponent {
  readonly confirm = inject(AdeniConfirmService);
  readonly titleId = "adeni-confirm-title";
  readonly bodyId = "adeni-confirm-body";

  private readonly panel = viewChild<ElementRef<HTMLElement>>("panel");

  constructor() {
    effect(() => {
      const open = this.confirm.dialog();
      if (!open) {
        return;
      }
      queueMicrotask(() => {
        const root = this.panel()?.nativeElement;
        const focusable = root?.querySelector<HTMLElement>("button.primary, button.danger, button");
        focusable?.focus();
      });
    });
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === "Escape") {
      event.preventDefault();
      this.confirm.settle(false);
    }
  }
}
