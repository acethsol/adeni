import { Component, inject } from "@angular/core";
import { AdeniFeedbackService } from "./adeni-feedback.service";

@Component({
  selector: "adeni-toast-host",
  standalone: true,
  template: `
    <div
      class="toast-host"
      [class.is-lifted]="feedback.loadingVisible()"
      aria-live="polite"
      aria-relevant="additions"
    >
      @for (toast of feedback.toasts(); track toast.id) {
        <div class="toast" [attr.data-tone]="toast.tone" role="status">
          <div class="toast-copy">
            <strong>{{ toast.title }}</strong>
            <p>{{ toast.message }}</p>
          </div>
          <button
            type="button"
            class="toast-close"
            aria-label="Dismiss"
            (click)="feedback.dismiss(toast.id)"
          >
            ×
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .toast-host {
      position: fixed;
      right: 1rem;
      bottom: 1rem;
      z-index: 1300;
      display: grid;
      gap: 0.55rem;
      width: min(22rem, calc(100vw - 2rem));
      pointer-events: none;
    }

    .toast-host.is-lifted {
      bottom: 5.5rem;
    }

    .toast {
      pointer-events: auto;
      display: flex;
      align-items: flex-start;
      gap: 0.65rem;
      padding: 0.85rem 0.9rem;
      border: 1px solid transparent;
      border-left-width: 4px;
      border-radius: var(--cds-radius-none, 0);
      background: #fff;
      box-shadow: 0 4px 16px rgb(0 0 0 / 14%);
      animation: toast-in 180ms ease;
    }

    :host-context([data-theme="dark"]) .toast {
      background: #262626;
      color: #f4f4f4;
      box-shadow: 0 4px 16px rgb(0 0 0 / 45%);
    }

    .toast[data-tone="success"] {
      border-left-color: #198038;
      border-color: #a7f0ba;
    }

    .toast[data-tone="error"] {
      border-left-color: #da1e28;
      border-color: #ffd7d9;
    }

    .toast[data-tone="info"] {
      border-left-color: #0f62fe;
      border-color: #d0e2ff;
    }

    :host-context([data-theme="dark"]) .toast[data-tone="success"] {
      border-color: rgb(25 128 56 / 45%);
    }

    :host-context([data-theme="dark"]) .toast[data-tone="error"] {
      border-color: rgb(218 30 40 / 45%);
    }

    :host-context([data-theme="dark"]) .toast[data-tone="info"] {
      border-color: rgb(15 98 254 / 45%);
    }

    .toast-copy {
      flex: 1;
      min-width: 0;
    }

    .toast strong {
      display: block;
      font-size: 0.88rem;
      font-weight: 700;
    }

    .toast p {
      margin: 0.2rem 0 0;
      font-size: 0.8rem;
      line-height: 1.35;
      color: var(--cds-text-secondary, #525252);
    }

    :host-context([data-theme="dark"]) .toast p {
      color: #c6c6c6;
    }

    .toast-close {
      flex-shrink: 0;
      width: 1.5rem;
      height: 1.5rem;
      border: 0;
      background: transparent;
      color: inherit;
      font-size: 1.15rem;
      line-height: 1;
      cursor: pointer;
      opacity: 0.7;
    }

    .toast-close:hover {
      opacity: 1;
    }

    @keyframes toast-in {
      from {
        opacity: 0;
        transform: translateY(6px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .toast {
        animation: none;
      }
    }
  `,
})
export class AdeniToastHostComponent {
  readonly feedback = inject(AdeniFeedbackService);
}
