import { Component, computed, input, output } from "@angular/core";

function listRangeLabel(page: number, pageSize: number, totalCount: number): string {
  if (totalCount <= 0) {
    return "0 results";
  }
  const start = (page - 1) * pageSize + 1;
  const end = Math.min(page * pageSize, totalCount);
  const maxPage = Math.max(1, Math.ceil(totalCount / pageSize));
  return `${start}–${end} of ${totalCount} · Page ${page} of ${maxPage}`;
}

@Component({
  selector: "adeni-list-pagination",
  standalone: true,
  template: `
    @if (totalPages() > 1) {
      <nav class="adeni-list-pagination" [attr.aria-label]="ariaLabel()">
        <button
          type="button"
          class="adeni-list-pagination-btn"
          [disabled]="page() <= 1 || loading()"
          (click)="goTo(page() - 1)"
        >
          Previous
        </button>
        <span class="adeni-list-pagination-meta">{{ rangeLabel() }}</span>
        <button
          type="button"
          class="adeni-list-pagination-btn"
          [disabled]="page() >= totalPages() || loading()"
          (click)="goTo(page() + 1)"
        >
          Next
        </button>
      </nav>
    } @else if (totalCount() > 0 && showMetaWhenSinglePage()) {
      <div class="adeni-list-pagination adeni-list-pagination--solo">
        <span class="adeni-list-pagination-meta">{{ rangeLabel() }}</span>
      </div>
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

  readonly pageChange = output<number>();

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / this.pageSize())),
  );

  readonly rangeLabel = computed(() =>
    listRangeLabel(this.page(), this.pageSize(), this.totalCount()),
  );

  goTo(next: number): void {
    const clamped = Math.min(Math.max(1, next), this.totalPages());
    if (clamped !== this.page()) {
      this.pageChange.emit(clamped);
    }
  }
}
