import { Component, computed, inject, input } from "@angular/core";
import { DomSanitizer } from "@angular/platform-browser";
import {
  type AdeniCarbonIconName,
  carbonIconMarkup,
} from "./carbon-icon";

@Component({
  selector: "adeni-carbon-icon",
  standalone: true,
  template: `<span class="glyph" [innerHTML]="markup()"></span>`,
  styles: `
    :host {
      display: inline-flex;
      flex-shrink: 0;
      line-height: 0;
      color: inherit;
    }
    .glyph {
      display: inline-flex;
      line-height: 0;
    }
    .glyph :is(svg) {
      display: block;
    }
  `,
})
export class AdeniCarbonIconComponent {
  readonly name = input.required<AdeniCarbonIconName>();
  readonly size = input<number | undefined>(undefined);

  private readonly sanitizer = inject(DomSanitizer);

  readonly markup = computed(() =>
    this.sanitizer.bypassSecurityTrustHtml(
      carbonIconMarkup(this.name(), { size: this.size() }),
    ),
  );
}
