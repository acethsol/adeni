import { NgTemplateOutlet } from "@angular/common";
import { Component, computed, contentChild, contentChildren, input, model } from "@angular/core";
import type { AdeniDataTableColumn, AdeniDataTableSortDirection } from "./adeni-list-view.types";
import { AdeniDataTableActionsDirective } from "./adeni-data-table-actions.directive";
import { AdeniDataTableCellDirective } from "./adeni-data-table-cell.directive";
import { isAdeniDataTableColumnSortable, sortDataRows } from "./sort-data-rows";

import { injectAdeniTranslate } from "./locale/adeni-i18n";
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
                <th
                  [class.align-right]="col.align === 'right'"
                  [class.adeni-data-table-sortable]="isSortable(col)"
                  [class.adeni-data-table-sorted]="sortColumnId() === col.id"
                  [attr.aria-sort]="ariaSort(col)"
                  scope="col"
                >
                  @if (isSortable(col)) {
                    <button
                      type="button"
                      class="adeni-data-table-sort-btn"
                      (click)="onHeaderSort(col)"
                    >
                      <span class="adeni-data-table-sort-label">{{ col.header }}</span>
                      <span class="adeni-data-table-sort-icon" aria-hidden="true"></span>
                    </button>
                  } @else {
                    {{ col.header }}
                  }
                </th>
              }
              @if (actions()) {
                <th class="align-right adeni-data-table-actions-head" scope="col">
                  {{ tr('common.actions') }}
                </th>
              }
            </tr>
          </thead>
          <tbody>
            @for (row of displayRows(); track trackRow()(row)) {
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
                  <td class="align-right adeni-data-table-actions">
                    <div class="adeni-data-table-actions-cell">
                      <ng-container
                        *ngTemplateOutlet="actionTpl.template; context: { $implicit: row }"
                      />
                    </div>
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  styleUrl: "./styles/_data-grid-table.scss",
})
export class AdeniDataTableComponent<T> {
  protected readonly tr = injectAdeniTranslate();
  readonly rows = input.required<T[]>();
  readonly columns = input.required<AdeniDataTableColumn<T>[]>();
  readonly trackRow = input.required<(row: T) => string>();
  readonly rowClass = input<(row: T) => string | undefined>();

  /** When false, rows are pre-sorted (card + table parity). Default true. */
  readonly sortRows = input(true);

  readonly sortColumnId = model<string | null>(null);
  readonly sortDirection = model<AdeniDataTableSortDirection>("asc");

  readonly actions = contentChild(AdeniDataTableActionsDirective);
  private readonly cellTemplates = contentChildren(AdeniDataTableCellDirective);

  readonly displayRows = computed(() => {
    if (!this.sortRows()) {
      return this.rows();
    }
    return sortDataRows(
      this.rows(),
      this.sortColumnId(),
      this.sortDirection(),
      this.columns(),
    );
  });

  readonly isSortable = isAdeniDataTableColumnSortable;

  cellTemplate(columnId: string): AdeniDataTableCellDirective | undefined {
    return this.cellTemplates().find((t) => t.columnId() === columnId);
  }

  ariaSort(column: AdeniDataTableColumn<T>): "none" | "ascending" | "descending" | undefined {
    if (!isAdeniDataTableColumnSortable(column)) {
      return undefined;
    }
    if (this.sortColumnId() !== column.id) {
      return "none";
    }
    return this.sortDirection() === "asc" ? "ascending" : "descending";
  }

  onHeaderSort(column: AdeniDataTableColumn<T>): void {
    if (!isAdeniDataTableColumnSortable(column)) {
      return;
    }
    if (this.sortColumnId() === column.id) {
      this.sortDirection.set(this.sortDirection() === "asc" ? "desc" : "asc");
      return;
    }
    this.sortColumnId.set(column.id);
    this.sortDirection.set("asc");
  }
}
