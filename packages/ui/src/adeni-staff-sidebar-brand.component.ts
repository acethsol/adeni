import { Component, input } from "@angular/core";
import { AdeniBrandLogoComponent } from "./adeni-brand-logo.component";

/** Portal/admin sidebar: approved horizontal SVG on dark + optional role caption. */
@Component({
  selector: "adeni-staff-sidebar-brand",
  standalone: true,
  imports: [AdeniBrandLogoComponent],
  template: `
    <adeni-brand-logo variant="horizontal" surface="dark" [size]="logoHeight()" />
    @if (caption()) {
      <span class="caption">{{ caption() }}</span>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.35rem;
      max-width: 100%;
    }
    .caption {
      font-size: 0.65rem;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      opacity: 0.72;
    }
  `,
})
export class AdeniStaffSidebarBrandComponent {
  readonly caption = input<string | null>(null);
  readonly logoHeight = input(44);
}
