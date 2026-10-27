import { Component, computed, input } from "@angular/core";

export type AdeniPillTone =
  | "neutral"
  | "success"
  | "warning"
  | "danger"
  | "info"
  | "brand"
  | "accent";

@Component({
  selector: "adeni-pill",
  standalone: true,
  template: `
    <span [class]="hostClass()">
      @if (dot()) {
        <span class="adeni-pill-dot" aria-hidden="true"></span>
      }
      <ng-content />
    </span>
  `,
  styleUrl: "./styles/_data-grid-pill.scss",
})
export class AdeniPillComponent {
  readonly tone = input<AdeniPillTone>("neutral");
  readonly compact = input(false);
  /** Status-style leading dot (bookable / live indicators). */
  readonly dot = input(false);

  readonly hostClass = computed(() => {
    const parts = ["adeni-pill", `adeni-pill--${this.tone()}`];
    if (this.compact()) {
      parts.push("adeni-pill--compact");
    }
    if (this.dot()) {
      parts.push("adeni-pill--dot");
    }
    return parts.join(" ");
  });
}
