import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  effect,
  input,
  viewChild,
} from "@angular/core";
import { DonutChart, LineChart, SimpleBarChart } from "@carbon/charts";
import { ScaleTypes, type ChartTabularData } from "@carbon/charts/interfaces";

export type DashboardCarbonChartKind = "bar" | "line" | "donut";

@Component({
  selector: "app-dashboard-carbon-chart",
  standalone: true,
  template: `<div #holder class="carbon-chart-holder" role="img" [attr.aria-label]="ariaLabel()"></div>`,
  styles: `
    :host {
      display: block;
      min-height: 12rem;
    }

    .carbon-chart-holder {
      width: 100%;
      min-height: 13.5rem;
    }

    .carbon-chart-holder :global(.cds--chart-holder),
    .carbon-chart-holder :global(.layout-child),
    .carbon-chart-holder :global(svg.layout-svg-wrapper) {
      background: transparent !important;
    }
  `,
})
export class DashboardCarbonChartComponent implements AfterViewInit, OnDestroy {
  readonly kind = input<DashboardCarbonChartKind>("bar");
  readonly data = input<ChartTabularData>([]);
  readonly ariaLabel = input("Chart");

  private readonly holderRef = viewChild<ElementRef<HTMLDivElement>>("holder");
  private chart: SimpleBarChart | LineChart | DonutChart | null = null;

  constructor() {
    effect(() => {
      const rows = this.data();
      if (this.chart) {
        this.chart.model.setData(rows);
        this.chart.update(true);
      }
    });
  }

  ngAfterViewInit(): void {
    this.renderChart();
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
    this.chart = null;
  }

  private resolveChartTheme(): "g10" | "g100" {
    if (typeof document === "undefined") {
      return "g10";
    }
    return document.documentElement.getAttribute("data-theme") === "dark" ? "g100" : "g10";
  }

  private renderChart(): void {
    const holder = this.holderRef()?.nativeElement;
    if (!holder) {
      return;
    }

    const rows = this.data();
    const theme = this.resolveChartTheme();
    const colorScale = {
      Appointments: "#7f56ff",
      Collected: "#13c2a3",
      Booked: "#13c2a3",
      Bookings: "#7f56ff",
    };
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
    };
    const lineOptions = {
      ...barOptions,
      height: "220px",
      grid: { x: { enabled: false }, y: { enabled: true } },
    };

    this.chart?.destroy();

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
          },
        });
        break;
      default:
        this.chart = new SimpleBarChart(holder, { data: rows, options: barOptions });
        break;
    }
  }
}
