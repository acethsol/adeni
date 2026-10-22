import { Component, computed, input } from "@angular/core";
import { normalizeDashboardWidgetWidth } from "@adeni/shared";

export type AdeniDashboardLayoutWireframeBlock = {
  w: number;
};

@Component({
  selector: "adeni-dashboard-layout-wireframe",
  standalone: true,
  template: `
    <div class="adeni-dash-wireframe" aria-hidden="true">
      @for (block of normalizedBlocks(); track $index) {
        <span
          class="adeni-dash-wireframe__block"
          [style.gridColumn]="'span ' + block"
        ></span>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }

    .adeni-dash-wireframe {
      display: grid;
      grid-template-columns: repeat(12, minmax(0, 1fr));
      gap: 0.2rem;
      width: 100%;
      margin: 0.35rem 0 0.15rem;
      padding: 0.35rem;
      border: 1px solid color-mix(in srgb, var(--border) 85%, transparent);
      background: color-mix(in srgb, var(--bg-subtle, #f4f4f4) 55%, var(--surface));
    }

    .adeni-dash-wireframe__block {
      display: block;
      min-height: 0.45rem;
      border-radius: 2px;
      background: color-mix(in srgb, var(--text-muted) 28%, var(--surface));
    }
  `,
})
export class AdeniDashboardLayoutWireframeComponent {
  readonly blocks = input<readonly AdeniDashboardLayoutWireframeBlock[]>([]);

  readonly normalizedBlocks = computed(() =>
    this.blocks().map((entry) => normalizeDashboardWidgetWidth(entry.w)),
  );
}
