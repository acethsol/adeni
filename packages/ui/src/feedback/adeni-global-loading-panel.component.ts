import { Component, inject } from "@angular/core";
import { AdeniFeedbackService } from "./adeni-feedback.service";

@Component({
  selector: "adeni-global-loading-panel",
  standalone: true,
  template: `
    @if (feedback.loadingVisible()) {
      <div
        class="load-backdrop"
        role="presentation"
        aria-hidden="true"
      >
        <div
          class="load-card"
          role="status"
          aria-live="polite"
          aria-busy="true"
          aria-atomic="true"
        >
          <span class="load-card__ring" aria-hidden="true"></span>
          <div class="load-spinner-wrap" aria-hidden="true">
            <span class="load-spinner"></span>
          </div>
          <div class="load-card__body">
            <strong>{{ feedback.loadingLabel() }}</strong>
            <p>{{ feedback.loadingDetail() }}</p>
          </div>
        </div>
      </div>
    }
  `,
  styleUrl: "./adeni-global-loading-panel.component.scss",
})
export class AdeniGlobalLoadingPanelComponent {
  readonly feedback = inject(AdeniFeedbackService);
}
