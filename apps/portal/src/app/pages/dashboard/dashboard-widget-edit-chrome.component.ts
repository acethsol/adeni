import { CdkDragHandle } from "@angular/cdk/drag-drop";
import { Component, input, output } from "@angular/core";
import {
  DASHBOARD_WIDGET_WIDTH_OPTIONS,
  type DashboardWidgetWidth,
} from "@adeni/shared";
import { AdeniCarbonIconComponent } from "@adeni/ui";

@Component({
  selector: "app-dashboard-widget-edit-chrome",
  standalone: true,
  imports: [AdeniCarbonIconComponent, CdkDragHandle],
  template: `
    <div class="widget-edit-chrome">
      <button type="button" class="widget-edit-chrome__drag" cdkDragHandle>
        <span aria-hidden="true">⋮⋮</span>
        <span class="sr-only">Drag to reorder</span>
      </button>

      @if (title()) {
        <span class="widget-edit-chrome__title">{{ title() }}</span>
      }

      <div
        class="widget-width-segment"
        role="group"
        [attr.aria-label]="'Width for ' + title()"
      >
        @for (option of widthOptions; track option.w) {
          <button
            type="button"
            [class.is-active]="width() === option.w"
            [attr.aria-pressed]="width() === option.w"
            [title]="option.title"
            (click)="onWidthSelect(option.w)"
          >
            {{ option.label }}
          </button>
        }
      </div>

      @if (removable()) {
        <button type="button" class="widget-edit-chrome__remove" (click)="remove.emit()">
          <adeni-carbon-icon name="close" [size]="16" />
          <span class="sr-only">Remove widget</span>
        </button>
      }
    </div>
  `,
  styleUrl: "./dashboard-widget-edit-chrome.component.scss",
})
export class DashboardWidgetEditChromeComponent {
  readonly title = input("");
  readonly width = input.required<DashboardWidgetWidth>();
  readonly removable = input(true);
  readonly widthOptions = DASHBOARD_WIDGET_WIDTH_OPTIONS;

  readonly remove = output<void>();
  readonly widthChange = output<DashboardWidgetWidth>();

  onWidthSelect(next: DashboardWidgetWidth): void {
    if (next === this.width()) {
      return;
    }
    this.widthChange.emit(next);
  }
}
