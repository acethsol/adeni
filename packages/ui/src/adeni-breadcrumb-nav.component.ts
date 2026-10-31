import { Component, input } from "@angular/core";
import { RouterLink } from "@angular/router";
import type { AdeniBreadcrumb } from "@adeni/shared";

@Component({
  selector: "adeni-breadcrumb-nav",
  standalone: true,
  imports: [RouterLink],
  host: {
    class: "crumb",
    role: "navigation",
    "[attr.aria-label]": "ariaLabel()",
  },
  template: `
    @for (crumb of crumbs(); track trackCrumb($index, crumb); let last = $last) {
      @if (last || !crumb.path) {
        <span [class.crumb-current]="last">{{ crumb.label }}</span>
      } @else {
        <a [routerLink]="crumb.path">{{ crumb.label }}</a>
      }
      @if (!last) {
        <span class="crumb-sep" aria-hidden="true"></span>
      }
    }
  `,
  styles: `
    :host {
      display: flex;
      align-items: center;
      gap: 0.55rem;
      min-width: 0;
      padding-left: 1rem;
      color: var(--text-muted);
      font-size: 0.875rem;
      font-weight: 600;
    }

    a {
      color: inherit;
      text-decoration: none;
    }

    a:hover {
      color: var(--text);
    }

    .crumb-sep {
      flex-shrink: 0;
      width: 0.35rem;
      height: 0.35rem;
      border-top: 1.5px solid currentColor;
      border-right: 1.5px solid currentColor;
      transform: rotate(45deg);
      opacity: 0.7;
    }

    .crumb-current {
      overflow: hidden;
      color: var(--text);
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `,
})
export class AdeniBreadcrumbNavComponent {
  readonly crumbs = input.required<readonly AdeniBreadcrumb[]>();
  readonly ariaLabel = input.required<string>();

  protected trackCrumb(index: number, crumb: AdeniBreadcrumb): string {
    return `${index}:${crumb.path ?? ""}:${crumb.label}`;
  }
}
