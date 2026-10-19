import { Component, computed, input, output } from "@angular/core";
import { AdeniCarbonIconComponent } from "./adeni-carbon-icon.component";
import type { AdeniCarbonIconName } from "./carbon-icon";

export type AdeniIconBtnVariant = "ghost" | "soft" | "outline" | "danger-soft";

@Component({
  selector: "adeni-icon-btn",
  standalone: true,
  imports: [AdeniCarbonIconComponent],
  template: `
    <button
      [type]="type()"
      [class]="hostClass()"
      [disabled]="disabled()"
      [title]="label()"
      [attr.aria-label]="label()"
      (click)="pressed.emit($event)"
    >
      <adeni-carbon-icon [name]="icon()" [size]="16" aria-hidden="true" />
      @if (showLabel()) {
        <span class="adeni-icon-btn-label">{{ label() }}</span>
      }
    </button>
  `,
  styleUrl: "./styles/_data-grid.scss",
})
export class AdeniIconBtnComponent {
  readonly icon = input.required<AdeniCarbonIconName>();
  readonly label = input.required<string>();
  readonly variant = input<AdeniIconBtnVariant>("ghost");
  readonly showLabel = input(false);
  readonly type = input<"button" | "submit">("button");
  readonly disabled = input(false);

  readonly pressed = output<MouseEvent>();

  readonly hostClass = computed(
    () => `adeni-icon-btn adeni-icon-btn--${this.variant()}`,
  );
}
