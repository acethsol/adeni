import { Component } from "@angular/core";

/** Horizontal row actions in data tables — styles live here, not on the page. */
@Component({
  selector: "adeni-row-action-bar",
  standalone: true,
  template: `
    <div class="adeni-row-action-bar">
      <ng-content />
    </div>
  `,
  styleUrl: "./styles/_data-grid.scss",
})
export class AdeniRowActionBarComponent {}
