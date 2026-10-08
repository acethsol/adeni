import { Component, inject } from "@angular/core";
import { DiscoverLoadingService } from "../core/services/discover-loading.service";

@Component({
  selector: "app-global-loading-panel",
  standalone: true,
  template: `
    @if (loading.visible()) {
      <div class="load-panel" role="status" aria-live="polite">
        <span class="load-spinner" aria-hidden="true"></span>
        <div>
          <strong>{{ loading.label() }}</strong>
          <p>{{ loading.detail() }}</p>
        </div>
      </div>
    }
  `,
  styles: `
    .load-panel {
      position: fixed;
      left: 50%;
      bottom: 1.25rem;
      z-index: 80;
      display: flex;
      align-items: center;
      gap: 0.85rem;
      min-width: min(22rem, calc(100vw - 2rem));
      padding: 0.85rem 1.05rem;
      border: 1px solid var(--cds-border-subtle);
      border-radius: var(--cds-radius-none);
      background: rgb(255 255 255 / 96%);
      box-shadow: 0 2px 6px rgb(0 0 0 / 20%);
      transform: translateX(-50%);
      animation: panel-in 180ms ease;
    }

    .load-panel strong {
      display: block;
      font-size: 0.9rem;
      color: var(--cds-text-primary);
    }

    .load-panel p {
      margin: 0.15rem 0 0;
      font-size: 0.78rem;
      color: var(--cds-text-secondary);
    }

    .load-spinner {
      width: 1.35rem;
      height: 1.35rem;
      border-radius: 50%;
      border: 2px solid var(--cds-gray-30);
      border-top-color: var(--interactive);
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
export class GlobalLoadingPanelComponent {
  readonly loading = inject(DiscoverLoadingService);
}
