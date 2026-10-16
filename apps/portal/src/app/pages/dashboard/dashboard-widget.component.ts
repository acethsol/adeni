import { CdkDragHandle } from "@angular/cdk/drag-drop";
import { Component, input, output } from "@angular/core";
import { AdeniCarbonIconComponent } from "@adeni/ui";

@Component({
  selector: "app-dashboard-widget",
  standalone: true,
  imports: [AdeniCarbonIconComponent, CdkDragHandle],
  template: `
    <article class="panel dashboard-widget" [class.dashboard-widget--embedded]="embedded()">
      @if (title() || editMode()) {
        <header class="dashboard-widget__head">
          <div class="dashboard-widget__title-row">
            @if (editMode()) {
              <span class="drag-handle" cdkDragHandle aria-hidden="true">⋮⋮</span>
            }
            @if (title()) {
              <h3>{{ title() }}</h3>
            }
            @if (editMode() && removable()) {
              <button type="button" class="btn icon-btn remove-btn" (click)="remove.emit()">
                <adeni-carbon-icon name="close" [size]="16" />
                <span class="sr-only">Remove widget</span>
              </button>
            }
          </div>
          @if (hint()) {
            <p class="hint">{{ hint() }}</p>
          }
        </header>
      }
      <div class="dashboard-widget__body">
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
  readonly editMode = input(false);
  readonly removable = input(true);
  readonly remove = output<void>();
}
