import { Component } from "@angular/core";

/** Wraps filter dropdowns in the toolbar — layout lives in `@adeni/ui`, not the page. */
@Component({
  selector: "adeni-list-filter-row",
  standalone: true,
  host: {
    class: "adeni-list-filter-row-host",
    "[attr.listFilters]": '""',
  },
  template: `
    <div class="adeni-filter-row">
      <ng-content />
    </div>
  `,
  styleUrl: "./styles/_data-grid-filter-row.scss",
})
export class AdeniListFilterRowComponent {}
