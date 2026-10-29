import { Component, input } from "@angular/core";
import type { AdeniBrandSurface } from "@adeni/brand";
import { AdeniBrandLogoComponent } from "./adeni-brand-logo.component";

/** Business/admin sidebar: horizontal lockup + caption aligned to the wordmark (not the mark). */
@Component({
  selector: "adeni-staff-sidebar-brand",
  standalone: true,
  imports: [AdeniBrandLogoComponent],
  template: `
    <div class="lockup" [attr.data-app]="app()" [style.--logo-px.px]="logoHeight()">
      <adeni-brand-logo variant="horizontal" [surface]="surface()" [size]="logoHeight()" />
      @if (caption()) {
        <span class="caption">{{ caption() }}</span>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      max-width: 100%;
    }

    .lockup {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.28rem;
      max-width: 100%;
    }

    .caption {
      margin: 0;
      /* Horizontal PNG lockup: wordmark starts after the symbol (~64% of rendered height). */
      padding-inline-start: calc(var(--logo-px, 32px) * 0.64 + 1px);
      max-width: 100%;
      font-size: 0.65rem;
      font-weight: 600;
      letter-spacing: 0.07em;
      text-transform: uppercase;
      line-height: 1.2;
      color: var(--text-muted);
      opacity: 0.88;
    }

    .lockup[data-app="admin"] .caption {
      color: color-mix(in srgb, var(--adeni-teal, #13c2a3) 38%, var(--text-muted));
    }
  `,
})
export class AdeniStaffSidebarBrandComponent {
  readonly caption = input<string | null>(null);
  readonly logoHeight = input(32);
  readonly surface = input<AdeniBrandSurface>("light");
  readonly app = input<"business" | "admin">("business");
}
