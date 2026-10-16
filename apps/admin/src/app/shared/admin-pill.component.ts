import { Component, computed, input } from "@angular/core";

export type AdminPillVariant =
  | "neutral"
  | "success"
  | "warning"
  | "danger"
  | "info"
  | "brand"
  | "accent";

@Component({
  selector: "admin-pill",
  standalone: true,
  template: `<span [class]="classes()">{{ label() }}</span>`,
  styles: `
    :host {
      display: inline-flex;
    }
  `,
})
export class AdminPillComponent {
  readonly label = input.required<string>();
  readonly variant = input<AdminPillVariant>("neutral");
  readonly compact = input(false);

  readonly classes = computed(
    () => `pill pill--${this.variant()}${this.compact() ? " pill--compact" : ""}`,
  );
}
