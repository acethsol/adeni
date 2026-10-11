import { Component, input } from "@angular/core";

/**
 * Standard page chrome for portal and admin (title, description, body slot).
 * Project primary actions with `pageActions` — they sit top-right of the header.
 */
@Component({
  selector: "app-portal-page",
  standalone: true,
  template: `
    <header class="page-head">
      <div class="page-head-copy">
        <h2>{{ title() }}</h2>
        @if (description()) {
          <p>{{ description() }}</p>
        }
      </div>
      <div class="page-head-actions">
        <ng-content select="[pageActions]" />
      </div>
    </header>
    <div class="page-body">
      <ng-content />
    </div>
  `,
  styles: `
    .page-head {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      justify-content: space-between;
      gap: 0.75rem 1rem;
    }
    .page-head-copy {
      min-width: 0;
      flex: 1 1 12rem;
    }
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
    .page-head-actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: flex-end;
      gap: 0.5rem;
      flex: 0 0 auto;
      margin-left: auto;
    }
    .page-head-actions:empty {
      display: none;
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
