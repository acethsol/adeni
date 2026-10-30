import { Component, inject, input, output } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink, RouterLinkActive } from "@angular/router";
import { map } from "rxjs";

export type AdeniTab = {
  id: string;
  label: string;
};

export type AdeniTabsLinkMode = "query" | "path" | "button";

/** Underline tabs — router query, child paths, or local button state. */
@Component({
  selector: "adeni-tabs",
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <div
      class="adeni-tabs"
      [class.adeni-tabs--vertical]="orientation() === 'vertical'"
      role="tablist"
    >
      @for (tab of tabs(); track tab.id) {
        @if (linkMode() === "path") {
          <a
            role="tab"
            [routerLink]="tab.id"
            routerLinkActive="active"
            #pathTab="routerLinkActive"
            [routerLinkActiveOptions]="{ exact: true }"
            [attr.aria-selected]="pathTab.isActive"
          >
            {{ tab.label }}
          </a>
        } @else if (linkMode() === "button") {
          <button
            type="button"
            role="tab"
            [attr.aria-selected]="activeId() === tab.id"
            [class.active]="activeId() === tab.id"
            (click)="selectButtonTab(tab.id)"
          >
            {{ tab.label }}
          </button>
        } @else {
          <a
            role="tab"
            [routerLink]="[]"
            [queryParams]="queryParamsFor(tab.id)"
            [attr.aria-selected]="isQueryActive(tab.id)"
            [class.active]="isQueryActive(tab.id)"
          >
            {{ tab.label }}
          </a>
        }
      }
    </div>
  `,
  styleUrl: "./adeni-tabs.component.scss",
})
export class AdeniTabsComponent {
  readonly tabs = input.required<AdeniTab[]>();
  /** `query` — `?tab=` on current route. `path` — relative child routes. `button` — local state via `activeId`. */
  readonly linkMode = input<AdeniTabsLinkMode>("query");
  /** Horizontal underline (default) or vertical rail for settings-style layouts. */
  readonly orientation = input<"horizontal" | "vertical">("horizontal");
  readonly queryParam = input("tab");
  /** Required when `linkMode` is `button`. */
  readonly activeId = input<string | null>(null);
  readonly activeIdChange = output<string>();

  private readonly route = inject(ActivatedRoute);
  private readonly selectedQueryTab = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get(this.queryParam()))),
    {
      initialValue: this.route.snapshot.queryParamMap.get(this.queryParam()),
    },
  );

  isQueryActive(id: string): boolean {
    const current = this.selectedQueryTab();
    if (!current) {
      return this.tabs()[0]?.id === id;
    }
    return current === id;
  }

  queryParamsFor(tabId: string): Record<string, string> {
    return { [this.queryParam()]: tabId };
  }

  selectButtonTab(tabId: string): void {
    this.activeIdChange.emit(tabId);
  }
}
