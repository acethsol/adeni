import { Component, inject } from "@angular/core";
import { AdeniBrandLogoComponent } from "../adeni-brand-logo.component";
import { AdeniFeedbackService } from "./adeni-feedback.service";

@Component({
  selector: "adeni-global-loading-panel",
  standalone: true,
  imports: [AdeniBrandLogoComponent],
  template: `
    @if (feedback.loadingVisible()) {
      <div class="load-backdrop" role="presentation" aria-hidden="true">
        <div
          class="load-card"
          role="status"
          aria-live="polite"
          aria-busy="true"
          aria-atomic="true"
        >
          <div class="load-card__accent" aria-hidden="true"></div>

          <div class="load-orbit" aria-hidden="true">
            <span class="load-orbit__ring load-orbit__ring--outer"></span>
            <span class="load-orbit__ring load-orbit__ring--inner"></span>
            <span class="load-orbit__core">
              <adeni-brand-logo variant="mark" [surface]="logoSurface()" [size]="22" />
            </span>
          </div>

          <div class="load-card__body">
            <div class="load-card__head">
              <strong>{{ feedback.loadingLabel() }}</strong>
              @if (feedback.loadingDepth() > 1) {
                <span class="load-card__depth">{{ depthLabel() }}</span>
              }
            </div>
            <p>{{ feedback.loadingDetail() }}</p>
            <div class="load-card__track" aria-hidden="true">
              <span class="load-card__track-fill"></span>
            </div>
          </div>
        </div>
      </div>
    }
  `,
  styleUrl: "./adeni-global-loading-panel.component.scss",
})
export class AdeniGlobalLoadingPanelComponent {
  readonly feedback = inject(AdeniFeedbackService);

  logoSurface(): "light" | "dark" {
    if (typeof document === "undefined") {
      return "light";
    }
    return document.documentElement.getAttribute("data-theme") === "dark" ? "dark" : "light";
  }

  depthLabel(): string {
    return this.feedback.text("toast.loadingDepth", { count: this.feedback.loadingDepth() });
  }
}
