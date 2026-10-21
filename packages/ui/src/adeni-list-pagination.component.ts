import { Component, computed, input, output } from "@angular/core";
import { AdeniCarbonIconComponent } from "./adeni-carbon-icon.component";
import { AdeniIconBtnComponent } from "./adeni-icon-btn.component";
import { buildListPaginationWindow } from "./list-pagination-window";

@Component({
  selector: "adeni-list-pagination",
  standalone: true,
  imports: [AdeniCarbonIconComponent, AdeniIconBtnComponent],
  template: `
    @if (visible()) {
      <footer
        class="adeni-list-pagination"
        [class.adeni-list-pagination--solo]="totalPages() <= 1"
        [class.adeni-list-pagination--embedded]="embedded()"
      >
        <div class="adeni-list-pagination__lead">
          <p class="adeni-list-pagination__summary">
            @if (totalCount() <= 0) {
              <span>Showing <strong>0</strong> results</span>
            } @else {
              <span>
                Showing
                <strong>{{ rangeStart() }}–{{ rangeEnd() }}</strong>
                of
                <strong>{{ totalCount() }}</strong>
              </span>
            }
          </p>
          @if (pageSizeOptions().length > 1) {
            <label class="adeni-list-pagination__page-size">
              <span class="adeni-list-pagination__page-size-label">Rows</span>
              <select
                class="adeni-list-pagination__page-size-select"
                [disabled]="loading()"
                [value]="pageSize()"
                (change)="onPageSizeSelect($event)"
              >
                @for (size of pageSizeOptions(); track size) {
                  <option [value]="size">{{ size }}</option>
                }
              </select>
            </label>
          }
        </div>

        @if (totalPages() > 1) {
          <nav class="adeni-list-pagination__nav" [attr.aria-label]="ariaLabel()">
            @if (showEdgeJumpersEffective()) {
              <adeni-icon-btn
                icon="arrow--left"
                label="First page"
                variant="outline"
                [dense]="true"
                [disabled]="page() <= 1 || loading()"
                (pressed)="goTo(1)"
              />
            }
            <button
              type="button"
              class="adeni-list-pagination-btn adeni-list-pagination-btn--nav"
              [disabled]="page() <= 1 || loading()"
              (click)="goTo(page() - 1)"
            >
              <adeni-carbon-icon name="chevron--left" [size]="16" aria-hidden="true" />
              <span class="adeni-list-pagination-btn__text">Previous</span>
            </button>

            <div class="adeni-list-pagination__pages" role="group" aria-label="Page numbers">
              @for (item of pageWindow(); track trackWindowItem($index, item)) {
                @if (item.kind === "gap") {
                  <span class="adeni-list-pagination__gap" aria-hidden="true">…</span>
                } @else {
                  <button
                    type="button"
                    class="adeni-list-pagination__page"
                    [class.adeni-list-pagination__page--current]="item.page === page()"
                    [disabled]="loading()"
                    [attr.aria-current]="item.page === page() ? 'page' : null"
                    [attr.aria-label]="'Page ' + item.page"
                    (click)="goTo(item.page)"
                  >
                    {{ item.page }}
                  </button>
                }
              }
            </div>

            <button
              type="button"
              class="adeni-list-pagination-btn adeni-list-pagination-btn--nav"
              [disabled]="page() >= totalPages() || loading()"
              (click)="goTo(page() + 1)"
            >
              <span class="adeni-list-pagination-btn__text">Next</span>
              <adeni-carbon-icon name="chevron--right" [size]="16" aria-hidden="true" />
            </button>
            @if (showEdgeJumpersEffective()) {
              <adeni-icon-btn
                icon="arrow--right"
                label="Last page"
                variant="outline"
                [dense]="true"
                [disabled]="page() >= totalPages() || loading()"
                (pressed)="goTo(totalPages())"
              />
            }
          </nav>
        }
      </footer>
    }
  `,
  styleUrl: "./styles/_data-grid.scss",
})
export class AdeniListPaginationComponent {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly loading = input(false);
  readonly ariaLabel = input("List pages");
  readonly showMetaWhenSinglePage = input(true);
  /** Drop outer chrome when nested inside cards or panes. */
  readonly embedded = input(false);
  /** First / last page icon jumpers when page count is large. */
  readonly showEdgeJumpers = input(true);
  readonly pageSizeOptions = input<number[]>([]);

  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / Math.max(1, this.pageSize()))),
  );

  readonly rangeStart = computed(() => {
    if (this.totalCount() <= 0) {
      return 0;
    }
    return (this.page() - 1) * this.pageSize() + 1;
  });

  readonly rangeEnd = computed(() => {
    if (this.totalCount() <= 0) {
      return 0;
    }
    return Math.min(this.page() * this.pageSize(), this.totalCount());
  });

  readonly pageWindow = computed(() =>
    buildListPaginationWindow(this.page(), this.totalPages(), 1),
  );

  readonly showEdgeJumpersEffective = computed(
    () => this.showEdgeJumpers() && this.totalPages() >= 6,
  );

  readonly visible = computed(() => {
    if (this.totalCount() <= 0) {
      return false;
    }
    if (this.totalPages() > 1) {
      return true;
    }
    return this.showMetaWhenSinglePage();
  });

  trackWindowItem(index: number, item: { kind: string; page?: number }): string | number {
    return item.kind === "gap" ? `gap-${index}` : item.page!;
  }

  goTo(next: number): void {
    const clamped = Math.min(Math.max(1, next), this.totalPages());
    if (clamped !== this.page()) {
      this.pageChange.emit(clamped);
    }
  }

  onPageSizeSelect(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    if (!Number.isFinite(value) || value <= 0 || value === this.pageSize()) {
      return;
    }
    this.pageSizeChange.emit(value);
  }
}
