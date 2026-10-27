import { Component, computed, input, output } from "@angular/core";
import { RouterLink } from "@angular/router";
import { AdeniCarbonIconComponent } from "./adeni-carbon-icon.component";
import type { AdeniCarbonIconName } from "./carbon-icon";

export type AdeniIconBtnVariant =
  | "ghost"
  | "soft"
  | "outline"
  | "danger-soft"
  | "table";

@Component({
  selector: "adeni-icon-btn",
  standalone: true,
  imports: [AdeniCarbonIconComponent, RouterLink],
  template: `
    @if (routerLink(); as link) {
      <a
        [routerLink]="link"
        [class]="hostClass()"
        [title]="label()"
        [attr.aria-label]="label()"
      >
        <adeni-carbon-icon [name]="icon()" [size]="16" aria-hidden="true" />
        @if (showLabel()) {
          <span class="adeni-icon-btn-label">{{ label() }}</span>
        }
      </a>
    } @else {
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
    }
  `,
  styleUrl: "./styles/_data-grid-icon-btn.scss",
})
export class AdeniIconBtnComponent {
  readonly icon = input.required<AdeniCarbonIconName>();
  readonly label = input.required<string>();
  readonly variant = input<AdeniIconBtnVariant>("ghost");
  readonly dense = input(false);
  readonly showLabel = input(false);
  readonly type = input<"button" | "submit">("button");
  readonly disabled = input(false);
  readonly routerLink = input<string | readonly string[] | null>(null);

  readonly pressed = output<MouseEvent>();

  readonly hostClass = computed(() => {
    const parts = ["adeni-icon-btn", `adeni-icon-btn--${this.variant()}`];
    if (this.dense()) {
      parts.push("adeni-icon-btn--dense");
    }
    return parts.join(" ");
  });
}
