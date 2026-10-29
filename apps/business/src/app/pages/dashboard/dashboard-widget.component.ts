import { Component, input, output } from "@angular/core";
import type { DashboardWidgetWidth } from "@adeni/shared";
import { DashboardWidgetEditChromeComponent } from "./dashboard-widget-edit-chrome.component";

@Component({
  selector: "app-dashboard-widget",
  standalone: true,
  imports: [DashboardWidgetEditChromeComponent],
  template: `
    <article
      class="dashboard-widget"
      [class.dashboard-widget--embedded]="embedded()"
      [class.dashboard-widget--customize]="customizeMode()"
    >
      @if (customizeMode()) {
        <app-dashboard-widget-edit-chrome
          [title]="title() ?? ''"
          [width]="layoutWidth()"
          [removable]="layoutRemovable()"
          (widthChange)="layoutWidthChange.emit($event)"
          (remove)="layoutRemove.emit()"
        />
      } @else if (title()) {
        <header class="dashboard-widget__head">
          <h3>{{ title() }}</h3>
          @if (hint()) {
            <p class="hint">{{ hint() }}</p>
          }
        </header>
      }
      <div class="dashboard-widget__body">
        @if (hint() && customizeMode()) {
          <p class="dashboard-widget__hint hint">{{ hint() }}</p>
        }
        <ng-content />
      </div>
    </article>
  `,
  styleUrl: "./dashboard-widget.component.scss",
})
export class DashboardWidgetComponent {
  readonly title = input<string | null>(null);
  readonly hint = input<string | null>(null);
  readonly embedded = input(false);
  readonly customizeMode = input(false);
  readonly layoutWidth = input<DashboardWidgetWidth>(12);
  readonly layoutRemovable = input(true);

  readonly layoutWidthChange = output<DashboardWidgetWidth>();
  readonly layoutRemove = output<void>();
}
