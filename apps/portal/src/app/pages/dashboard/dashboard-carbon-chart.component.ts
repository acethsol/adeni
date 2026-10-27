import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  computed,
  effect,
  inject,
  input,
  viewChild,
} from "@angular/core";
import { DonutChart, LineChart, SimpleBarChart } from "@carbon/charts";
import { PortalThemeService } from "../../core/portal-theme.service";
import { ScaleTypes, type ChartTabularData } from "@carbon/charts/interfaces";

export type DashboardCarbonChartKind = "bar" | "line" | "donut";

@Component({
  selector: "app-dashboard-carbon-chart",
  standalone: true,
  template: `
    <div #holder class="carbon-chart-holder" role="img" [attr.aria-label]="ariaLabel()"></div>
  `,
  styles: `
    :host {
      display: block;
      min-height: 12rem;
    }

    .carbon-chart-holder {
      width: 100%;
      min-height: 13.5rem;
    }

    /* Carbon injects DOM outside Angular view encapsulation; :global() is not valid Angular CSS. */
    :host ::ng-deep .carbon-chart-holder.cds--chart-holder,
    :host ::ng-deep .carbon-chart-holder .layout-child,
    :host ::ng-deep .carbon-chart-holder svg.layout-svg-wrapper {
      background: transparent !important;
    }
  `,
})
export class DashboardCarbonChartComponent implements AfterViewInit, OnDestroy {
  readonly kind = input<DashboardCarbonChartKind>("bar");
  readonly data = input<ChartTabularData>([]);
  readonly ariaLabel = input("Chart");

  private readonly portalTheme = inject(PortalThemeService);
  private readonly holderRef = viewChild<ElementRef<HTMLDivElement>>("holder");
  private chart: SimpleBarChart | LineChart | DonutChart | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private observedHolder: HTMLDivElement | null = null;
  /** Carbon theme/kind are fixed at create — remount in place when either changes. */
  readonly chartSurfaceKey = computed(() => `${this.portalTheme.theme()}:${this.kind()}`);
  private mountedSurfaceKey: string | null = null;

  constructor() {
    // One effect only: a separate teardown effect plus an @for-remounted holder raced and left
    // the chart attached to a detached node (blank card) after a light/dark toggle.
    effect(() => {
      const surfaceKey = this.chartSurfaceKey();
      const rows = this.data();
      const holder = this.holderRef()?.nativeElement;
      if (!holder) {
        return;
      }

      this.ensureResizeObserver(holder);

      if (!this.chart || this.mountedSurfaceKey !== surfaceKey) {
        this.teardownChart();
        this.mountChart(holder, rows);
        this.mountedSurfaceKey = surfaceKey;
        this.scheduleChartUpdate(holder);
        return;
      }

      this.chart.model.setData(rows);
      this.scheduleChartUpdate(holder);
    });
  }

  ngAfterViewInit(): void {
    const holder = this.holderRef()?.nativeElement;
    if (holder) {
      this.ensureResizeObserver(holder);
    }
  }

  private ensureResizeObserver(holder: HTMLDivElement): void {
    if (this.observedHolder === holder) {
      return;
    }

    this.resizeObserver?.disconnect();
    this.observedHolder = holder;

    if (typeof ResizeObserver === "undefined") {
      return;
    }

    this.resizeObserver = new ResizeObserver(() => {
      this.scheduleChartUpdate(holder);
    });
    this.resizeObserver.observe(holder);
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
    this.observedHolder = null;
    this.teardownChart();
  }

  private teardownChart(): void {
    this.chart?.destroy();
    this.chart = null;
    this.mountedSurfaceKey = null;
  }

  private resolveChartTheme(): "g10" | "g100" {
    return this.portalTheme.theme() === "dark" ? "g100" : "g10";
  }

  private scheduleChartUpdate(holder: HTMLDivElement): void {
    if (!this.chart) {
      return;
    }

    const runUpdate = (): void => {
      if (!this.chart || !holder.isConnected) {
        return;
      }
      if (holder.clientWidth <= 0) {
        requestAnimationFrame(runUpdate);
        return;
      }
      this.chart.update(true);
    };

    queueMicrotask(runUpdate);
  }

  /** Brand palette keyed by actual `group` values (i18n-safe). */
  private buildColorScale(rows: ChartTabularData): Record<string, string> {
    const palette = ["#7f56ff", "#13c2a3", "#f5a623", "#e85d75"];
    const groups = [...new Set(rows.map((row) => String(row["group"] ?? "")))].filter((g) => g.length > 0);
    return Object.fromEntries(groups.map((group, index) => [group, palette[index % palette.length]!]));
  }

  private mountChart(holder: HTMLDivElement, rows: ChartTabularData): void {
    const theme = this.resolveChartTheme();
    const colorScale = this.buildColorScale(rows);
    const barOptions = {
      title: "",
      axes: {
        bottom: {
          mapsTo: "key",
          scaleType: ScaleTypes.LABELS,
        },
        left: {
          mapsTo: "value",
          scaleType: ScaleTypes.LINEAR,
        },
      },
      height: "240px",
      theme,
      color: { scale: colorScale },
      grid: {
        x: { enabled: false },
        y: { enabled: true, numberOfTicks: 4 },
      },
      legend: { enabled: false },
      toolbar: { enabled: false },
      tooltip: { enabled: true },
    };
    const lineOptions = {
      ...barOptions,
      height: "220px",
      grid: { x: { enabled: false }, y: { enabled: true } },
    };

    this.chart?.destroy();
    holder.replaceChildren();

    switch (this.kind()) {
      case "line":
        this.chart = new LineChart(holder, { data: rows, options: lineOptions });
        break;
      case "donut":
        this.chart = new DonutChart(holder, {
          data: rows,
          options: {
            title: "",
            height: "220px",
            theme,
            color: {
              pairing: {
                option: 2,
              },
            },
            legend: { alignment: "center" },
            toolbar: { enabled: false },
            tooltip: { enabled: true },
          },
        });
        break;
      default:
        this.chart = new SimpleBarChart(holder, { data: rows, options: barOptions });
        break;
    }
  }
}
