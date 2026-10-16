import { Component, computed, input, output } from "@angular/core";
import { RouterLink } from "@angular/router";
import { AdeniCarbonIconComponent } from "./adeni-carbon-icon.component";
import type { AdeniCarbonIconName } from "./carbon-icon";

/** Staff portal + admin: Carbon icon + label, `.btn` variants from `_staff-layout.scss`. */
export type AdeniStaffButtonVariant = "default" | "primary" | "danger";
export type AdeniStaffButtonIconPosition = "start" | "end";

@Component({
  selector: "adeni-staff-btn",
  standalone: true,
  imports: [AdeniCarbonIconComponent, RouterLink],
  template: `
    @if (routerLink(); as link) {
      <a [class]="hostClass()" [routerLink]="link" [queryParams]="queryParams() ?? undefined">
        @if (iconPosition() === "start") {
          <adeni-carbon-icon [name]="icon()" [size]="16" />
        }
        <ng-content />
        @if (iconPosition() === "end") {
          <adeni-carbon-icon [name]="icon()" [size]="16" />
        }
      </a>
    } @else {
      <button
        [type]="type()"
        [class]="hostClass()"
        [disabled]="disabled()"
        (click)="pressed.emit($event)"
      >
        @if (iconPosition() === "start") {
          <adeni-carbon-icon [name]="icon()" [size]="16" />
        }
        <ng-content />
        @if (iconPosition() === "end") {
          <adeni-carbon-icon [name]="icon()" [size]="16" />
        }
      </button>
    }
  `,
  styles: `
    :host {
      display: inline-flex;
      max-width: 100%;
    }
  `,
})
export class AdeniStaffButtonComponent {
  readonly icon = input.required<AdeniCarbonIconName>();
  readonly iconPosition = input<AdeniStaffButtonIconPosition>("start");
  readonly variant = input<AdeniStaffButtonVariant>("default");
  readonly type = input<"button" | "submit">("button");
  readonly disabled = input(false);
  readonly routerLink = input<string | readonly string[] | null>(null);
  readonly queryParams = input<Record<string, unknown> | null>(null);

  readonly pressed = output<MouseEvent>();

  readonly hostClass = computed(() => {
    const parts = ["btn"];
    const variant = this.variant();
    if (variant === "primary") {
      parts.push("btn-primary");
    } else if (variant === "danger") {
      parts.push("btn-danger");
    }
    return parts.join(" ");
  });
}
