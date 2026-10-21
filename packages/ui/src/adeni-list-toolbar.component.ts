import { Component, input, model, signal } from "@angular/core";
import { AdeniCarbonIconComponent } from "./adeni-carbon-icon.component";
import type { AdeniCarbonIconName } from "./carbon-icon";
import { persistListViewMode, type AdeniListViewMode } from "./adeni-list-view.types";

@Component({
  selector: "adeni-list-toolbar",
  standalone: true,
  imports: [AdeniCarbonIconComponent],
  template: `
    <div class="adeni-list-toolbar-shell">
      <div class="adeni-list-toolbar">
        <div class="adeni-list-toolbar-primary">
          @if (showSearch()) {
            <div
              class="adeni-list-search"
              [class.has-value]="searchQuery().length > 0"
              [class.is-focused]="searchFocused()"
            >
              <span class="adeni-list-search-icon" aria-hidden="true">
                <adeni-carbon-icon name="search" [size]="18" />
              </span>
              <label class="adeni-list-search-field">
                <span class="visually-hidden">{{ searchPlaceholder() }}</span>
                <input
                  type="search"
                  [placeholder]="searchPlaceholder()"
                  [value]="searchQuery()"
                  (input)="onSearchChange($any($event.target).value)"
                  (focus)="searchFocused.set(true)"
                  (blur)="searchFocused.set(false)"
                />
              </label>
              @if (searchQuery().length > 0) {
                <button
                  type="button"
                  class="adeni-list-search-clear"
                  title="Clear search"
                  aria-label="Clear search"
                  (click)="clearSearch()"
                >
                  <adeni-carbon-icon name="close" [size]="16" />
                </button>
              }
            </div>
          }

          <ng-content select="[listFilters]" />
        </div>

        <div class="adeni-list-toolbar-secondary">
          <span class="adeni-list-count-pill" aria-live="polite">
            <span class="adeni-list-count-value">{{ count() }}</span>
            @if (countTotal() !== null && countTotal()! > count()) {
              <span class="adeni-list-count-of">of {{ countTotal() }}</span>
            }
            <span class="adeni-list-count-label">
              {{ count() === 1 ? singularCountLabel() : pluralCountLabel() }}
            </span>
          </span>

          <div class="adeni-list-toolbar-end">
            <ng-content select="[listExports]" />

            @if (showViewSwitcher()) {
              <div class="adeni-view-switcher" role="group" aria-label="View mode">
                <button
                  type="button"
                  class="adeni-view-switcher-btn"
                  [class.active]="viewMode() === 'cards'"
                  [attr.aria-pressed]="viewMode() === 'cards'"
                  [title]="viewModeCardsLabel() + ' view'"
                  (click)="setViewMode('cards')"
                >
                  <adeni-carbon-icon [name]="viewModeCardsIcon()" [size]="16" aria-hidden="true" />
                  <span>{{ viewModeCardsLabel() }}</span>
                </button>
                <button
                  type="button"
                  class="adeni-view-switcher-btn"
                  [class.active]="viewMode() === 'table'"
                  [attr.aria-pressed]="viewMode() === 'table'"
                  [title]="viewModeTableLabel() + ' view'"
                  (click)="setViewMode('table')"
                >
                  <adeni-carbon-icon [name]="viewModeTableIcon()" [size]="16" aria-hidden="true" />
                  <span>{{ viewModeTableLabel() }}</span>
                </button>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `,
  styleUrl: "./styles/_data-grid.scss",
})
export class AdeniListToolbarComponent {
  readonly storageKey = input<string | null>(null);
  readonly viewMode = model<AdeniListViewMode>("cards");

  readonly count = input(0);
  /** When set and greater than `count`, shows “N of total” (paginated client lists). */
  readonly countTotal = input<number | null>(null);
  readonly singularCountLabel = input("item");
  readonly pluralCountLabel = input("items");

  readonly showSearch = input(true);
  readonly searchPlaceholder = input("Search");
  readonly searchQuery = model("");

  readonly showViewSwitcher = input(true);
  readonly viewModeCardsLabel = input("Cards");
  readonly viewModeTableLabel = input("List");
  readonly viewModeCardsIcon = input<AdeniCarbonIconName>("grid");
  readonly viewModeTableIcon = input<AdeniCarbonIconName>("table");

  readonly searchFocused = signal(false);

  onSearchChange(value: string): void {
    this.searchQuery.set(value);
  }

  clearSearch(): void {
    this.searchQuery.set("");
  }

  setViewMode(mode: AdeniListViewMode): void {
    if (this.viewMode() === mode) {
      return;
    }
    this.viewMode.set(mode);
    const key = this.storageKey();
    if (key) {
      persistListViewMode(key, mode);
    }
  }
}
