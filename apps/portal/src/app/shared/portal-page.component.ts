import { Component, input } from "@angular/core";

@Component({
  selector: "app-portal-page",
  standalone: true,
  template: `
    <header class="page-head">
      <h2>{{ title() }}</h2>
      @if (description()) {
        <p>{{ description() }}</p>
      }
    </header>
    <div class="page-body">
      <ng-content />
    </div>
  `,
  styles: `
    .page-head h2 {
      margin: 0;
      font-size: 1.15rem;
      font-weight: 700;
    }
    .page-head p {
      margin: 0.35rem 0 0;
      color: var(--text-muted);
      font-size: 0.925rem;
    }
    .page-body {
      margin-top: 1.25rem;
    }
  `,
})
export class PortalPageComponent {
  readonly title = input.required<string>();
  readonly description = input<string>("");
}
