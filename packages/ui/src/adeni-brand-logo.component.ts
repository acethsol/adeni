import { Component, computed, input } from "@angular/core";
import {
  ADENI_BRAND_NAME,
  resolveAdeniBrandImagePath,
  type AdeniBrandLogoVariant,
  type AdeniBrandSurface,
} from "@adeni/brand";

@Component({
  selector: "adeni-brand-logo",
  standalone: true,
  template: `<img [src]="src()" [alt]="alt()" [style.height.px]="size()" />`,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      flex-shrink: 0;
      line-height: 0;
    }
    img {
      display: block;
      width: auto;
      height: auto;
      object-fit: contain;
    }
  `,
})
export class AdeniBrandLogoComponent {
  readonly size = input(32);
  readonly alt = input(ADENI_BRAND_NAME);
  readonly variant = input<AdeniBrandLogoVariant>("mark");
  readonly surface = input<AdeniBrandSurface>("light");

  readonly src = computed(() => resolveAdeniBrandImagePath(this.variant(), this.surface()));
}
