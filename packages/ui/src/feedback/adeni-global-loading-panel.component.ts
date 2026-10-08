import { Component, inject } from "@angular/core";
import { AdeniFeedbackService } from "./adeni-feedback.service";

@Component({
  selector: "adeni-global-loading-panel",
  standalone: true,
  template: `
    @if (feedback.loadingVisible()) {
      <div class="load-panel" role="status" aria-live="polite" aria-busy="true">
        <span class="load-spinner" aria-hidden="true"></span>
        <div>
          <strong>{{ feedback.loadingLabel() }}</strong>
          <p>{{ feedback.loadingDetail() }}</p>
        </div>
      </div>
    }
  `,
  styles: `
    .load-panel {
      position: fixed;
      left: 50%;
      bottom: 1.25rem;
      z-index: 1200;
      display: flex;
      align-items: center;
      gap: 0.85rem;
      min-width: min(22rem, calc(100vw - 2rem));
      padding: 0.85rem 1.05rem;
      border: 1px solid var(--cds-border-subtle, #e0e0e0);
      border-radius: var(--cds-radius-none, 0);
      background: rgb(255 255 255 / 96%);
      color: var(--cds-text-primary, #161616);
      box-shadow: 0 2px 6px rgb(0 0 0 / 20%);
      transform: translateX(-50%);
      animation: panel-in 180ms ease;
    }

    :host-context([data-theme="dark"]) .load-panel {
      background: rgb(38 38 38 / 96%);
      border-color: rgb(255 255 255 / 14%);
      color: #f4f4f4;
    }

    .load-panel strong {
      display: block;
      font-size: 0.9rem;
    }

    .load-panel p {
      margin: 0.15rem 0 0;
      font-size: 0.78rem;
      color: var(--cds-text-secondary, #525252);
    }

    :host-context([data-theme="dark"]) .load-panel p {
      color: #c6c6c6;
    }

    .load-spinner {
      width: 1.35rem;
      height: 1.35rem;
      border-radius: 50%;
      border: 2px solid var(--cds-gray-30, #c6c6c6);
      border-top-color: var(--interactive, #0f62fe);
      animation: spin 0.75s linear infinite;
      flex-shrink: 0;
    }

    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }

    @keyframes panel-in {
      from {
        opacity: 0;
        transform: translate(-50%, 8px);
      }
      to {
        opacity: 1;
        transform: translate(-50%, 0);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .load-panel,
      .load-spinner {
        animation: none;
      }
    }
  `,
})
export class AdeniGlobalLoadingPanelComponent {
  readonly feedback = inject(AdeniFeedbackService);
}
