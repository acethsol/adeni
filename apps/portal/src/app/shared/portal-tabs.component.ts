import { Component, inject, input } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { map } from "rxjs";
import type { PortalTab } from "../core/portal-nav";

@Component({
  selector: "app-portal-tabs",
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="tabs" role="tablist">
      @for (tab of tabs(); track tab.id) {
        <a
          role="tab"
          [routerLink]="[]"
          [queryParams]="{ tab: tab.id }"
          [attr.aria-selected]="isActive(tab.id)"
          [class.active]="isActive(tab.id)"
        >
          {{ tab.label }}
        </a>
      }
    </div>
  `,
  styles: `
    .tabs {
      display: flex;
      gap: 0.25rem;
      margin: 0 0 1rem;
      border-bottom: 1px solid var(--border);
    }

    a {
      margin-bottom: -1px;
      padding: 0.55rem 0.85rem;
      border-bottom: 2px solid transparent;
      color: var(--text-muted);
      font-size: 0.875rem;
      font-weight: 650;
      text-decoration: none;
    }

    a:hover {
      color: var(--text);
    }

    a.active {
      border-bottom-color: var(--adeni-purple, #7f56ff);
      color: var(--text);
    }
  `,
})
export class PortalTabsComponent {
  readonly tabs = input.required<PortalTab[]>();
  private readonly route = inject(ActivatedRoute);
  private readonly selected = toSignal(this.route.queryParamMap.pipe(map((params) => params.get("tab"))), {
    initialValue: this.route.snapshot.queryParamMap.get("tab"),
  });

  isActive(id: string): boolean {
    const current = this.selected();
    if (!current) {
      return this.tabs()[0]?.id === id;
    }
    return current === id;
  }
}
