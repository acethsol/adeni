import { NgTemplateOutlet } from "@angular/common";
import { Component, contentChild, contentChildren, input } from "@angular/core";
import type { AdeniDataTableColumn } from "./adeni-list-view.types";
import { AdeniDataTableActionsDirective } from "./adeni-data-table-actions.directive";
import { AdeniDataTableCellDirective } from "./adeni-data-table-cell.directive";

@Component({
  selector: "adeni-data-table",
  standalone: true,
  imports: [NgTemplateOutlet],
  template: `
    <div class="adeni-data-table-shell">
      <div class="adeni-data-table-wrap">
        <table class="adeni-data-table">
          <thead>
            <tr>
              @for (col of columns(); track col.id) {
                <th [class.align-right]="col.align === 'right'" scope="col">
                  {{ col.header }}
                </th>
              }
              @if (actions()) {
                <th class="align-right adeni-data-table-actions-head" scope="col">
                  Actions
                </th>
              }
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track trackRow()(row)) {
              <tr [class]="rowClass()?.(row) ?? ''">
                @for (col of columns(); track col.id) {
                  <td [class.align-right]="col.align === 'right'">
                    @if (cellTemplate(col.id); as cellTpl) {
                      <ng-container
                        *ngTemplateOutlet="cellTpl.template; context: { $implicit: row }"
                      />
                    } @else {
                      <span class="adeni-data-table-text">{{ col.cell(row) }}</span>
                    }
                  </td>
                }
                @if (actions(); as actionTpl) {
                  <td class="adeni-data-table-actions">
                    <ng-container
                      *ngTemplateOutlet="actionTpl.template; context: { $implicit: row }"
                    />
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  styleUrl: "./styles/_data-grid.scss",
})
export class AdeniDataTableComponent<T> {
  readonly rows = input.required<T[]>();
  readonly columns = input.required<AdeniDataTableColumn<T>[]>();
  readonly trackRow = input.required<(row: T) => string>();
  readonly rowClass = input<(row: T) => string | undefined>();

  readonly actions = contentChild(AdeniDataTableActionsDirective);
  private readonly cellTemplates = contentChildren(AdeniDataTableCellDirective);

  cellTemplate(columnId: string): AdeniDataTableCellDirective | undefined {
    return this.cellTemplates().find((t) => t.columnId() === columnId);
  }
}
