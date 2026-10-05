import { Component, computed, input } from "@angular/core";
import { AdeniBrandLogoComponent } from "./adeni-brand-logo.component";
import type { AdeniBrandSurface } from "@adeni/brand";

/**
 * Primary nav lockup — uses shipped horizontal SVG (mark + wordmark as one asset).
 * Do not substitute HTML text for the wordmark.
 */
@Component({
  selector: "adeni-brand-lockup",
  standalone: true,
  imports: [AdeniBrandLogoComponent],
  template: `<adeni-brand-logo variant="horizontal" [surface]="surface()" [size]="logoHeight()" />`,
  styles: `:host { display: inline-flex; align-items: center; }`,
})
export class AdeniBrandLockupComponent {
  readonly height = input(40);
  readonly surface = input<AdeniBrandSurface>("light");
  /** @deprecated use height — kept for existing templates */
  readonly markSize = input<number | undefined>(undefined);

  readonly logoHeight = computed(() => this.markSize() ?? this.height());
}
