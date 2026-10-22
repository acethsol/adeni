import { Component, input, output } from "@angular/core";
import type { DashboardLayoutPresetId } from "@adeni/shared";
import { AdeniCarbonIconComponent } from "../adeni-carbon-icon.component";
import type { AdeniCarbonIconName } from "../carbon-icon";
import { AdeniPillComponent } from "../adeni-pill.component";
import { injectAdeniTranslate } from "../locale/adeni-i18n";
import {
  AdeniDashboardLayoutWireframeComponent,
  type AdeniDashboardLayoutWireframeBlock,
} from "./adeni-dashboard-layout-wireframe.component";

@Component({
  selector: "adeni-dashboard-layout-preset-card",
  standalone: true,
  imports: [
    AdeniCarbonIconComponent,
    AdeniPillComponent,
    AdeniDashboardLayoutWireframeComponent,
  ],
  template: `
    <button
      type="button"
      [class]="cardClass()"
      [disabled]="disabled()"
      (click)="selected.emit()"
    >
      @if (recommended()) {
        <adeni-pill tone="brand" [compact]="true" class="layout-preset-card__badge layout-preset-card__badge--recommended">
          {{ recommendedLabel() }}
        </adeni-pill>
      }
      @if (active()) {
        <adeni-pill tone="success" [compact]="true" [dot]="true" class="layout-preset-card__badge layout-preset-card__badge--active">
          {{ activeLabel() }}
        </adeni-pill>
      }
      <span class="layout-preset-card__icon" aria-hidden="true">
        <adeni-carbon-icon [name]="icon()" [size]="22" />
      </span>
      <span class="layout-preset-card__focus">{{ focus() }}</span>
      <span class="layout-preset-card__label">{{ label() }}</span>
      <span class="layout-preset-card__desc">{{ description() }}</span>
      @if (marketLine()) {
        <span class="layout-preset-card__market">{{ marketLine() }}</span>
      }
      <adeni-dashboard-layout-wireframe [blocks]="wireframeBlocks()" />
      <span class="layout-preset-card__meta">{{ metaLine() }}</span>
      @if (widgetTitles().length > 0) {
        <span class="layout-preset-card__chips" [attr.aria-label]="chipsAriaLabel()">
          @for (title of widgetTitles().slice(0, 5); track title) {
            <span class="layout-preset-chip">{{ title }}</span>
          }
          @if (widgetTitles().length > 5) {
            <span class="layout-preset-chip layout-preset-chip--more">
              +{{ widgetTitles().length - 5 }} more
            </span>
          }
        </span>
      } @else if (emptyPlanHint()) {
        <span class="layout-preset-card__empty hint">{{ emptyPlanHint() }}</span>
      }
      <span class="layout-preset-card__cta">
        {{ ctaLabel() }}
        <adeni-carbon-icon name="arrow--right" [size]="16" />
      </span>
    </button>
  `,
  styleUrl: "../styles/_dashboard-layout-presets.scss",
})
export class AdeniDashboardLayoutPresetCardComponent {
  private readonly tr = injectAdeniTranslate();
  readonly presetId = input.required<DashboardLayoutPresetId>();
  readonly icon = input.required<AdeniCarbonIconName>();
  readonly focus = input.required<string>();
  readonly label = input.required<string>();
  readonly description = input.required<string>();
  readonly marketLine = input<string | null>(null);
  readonly metaLine = input.required<string>();
  readonly widgetTitles = input<readonly string[]>([]);
  readonly wireframeBlocks = input<readonly AdeniDashboardLayoutWireframeBlock[]>([]);
  readonly recommended = input(false);
  readonly active = input(false);
  readonly disabled = input(false);
  readonly recommendedLabel = input(this.tr("ui.ui.presetRecommended"));
  readonly activeLabel = input(this.tr("ui.ui.presetCurrentLayout"));
  readonly emptyPlanHint = input<string | null>(null);
  readonly ctaLabel = input.required<string>();
  readonly chipsAriaLabel = input(this.tr("ui.ui.presetIncludedWidgets"));

  readonly selected = output<void>();

  cardClass(): string {
    const parts = ["layout-preset-card", `layout-preset-card--${this.presetId()}`];
    if (this.active()) {
      parts.push("layout-preset-card--active");
    }
    return parts.join(" ");
  }
}
