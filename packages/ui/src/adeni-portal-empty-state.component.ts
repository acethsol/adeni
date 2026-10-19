import { booleanAttribute, Component, HostBinding, input } from "@angular/core";
import { AdeniCarbonIconComponent } from "./adeni-carbon-icon.component";
import type { AdeniCarbonIconName } from "./carbon-icon";

@Component({
  selector: "adeni-portal-empty-state",
  standalone: true,
  imports: [AdeniCarbonIconComponent],
  template: `
    <div
      class="portal-empty"
      [class.portal-empty--compact]="compact()"
      [class.portal-empty--embedded]="embedded()"
      role="status"
    >
      <div class="portal-empty__icon" aria-hidden="true">
        <adeni-carbon-icon [name]="icon()" [size]="compact() || embedded() ? 28 : 40" />
      </div>
      <h3 class="portal-empty__title">{{ title() }}</h3>
      @if (description()) {
        <p class="portal-empty__desc">{{ description() }}</p>
      }
      <div class="portal-empty__actions">
        <ng-content />
      </div>
    </div>
  `,
  styles: `
    .portal-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 16rem;
      padding: 2.25rem 1.5rem;
      text-align: center;
      border: 1px solid var(--border);
      background:
        radial-gradient(
          ellipse 55% 70% at 50% 0%,
          color-mix(in srgb, var(--interactive, #7f56ff) 11%, transparent),
          transparent 62%
        ),
        var(--surface);
    }

    .portal-empty--compact {
      min-height: 11rem;
      padding: 1.75rem 1.25rem;
    }

    .portal-empty--embedded {
      min-height: 0;
      padding: 1.25rem 0.75rem;
      border: 0;
      background: transparent;
    }

    .portal-empty__icon {
      display: grid;
      place-items: center;
      width: 4.5rem;
      height: 4.5rem;
      margin-bottom: 1rem;
      border-radius: 50%;
      color: var(--interactive, #7f56ff);
      background: color-mix(in srgb, var(--interactive, #7f56ff) 10%, var(--surface));
      box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--interactive, #7f56ff) 16%, transparent);
    }

    .portal-empty--compact .portal-empty__icon,
    .portal-empty--embedded .portal-empty__icon {
      width: 3.5rem;
      height: 3.5rem;
      margin-bottom: 0.75rem;
    }

    .portal-empty__title {
      margin: 0;
      font-size: 1.125rem;
      font-weight: 700;
      letter-spacing: -0.02em;
      line-height: 1.25;
      color: var(--text);
    }

    .portal-empty--compact .portal-empty__title {
      font-size: 1rem;
    }

    .portal-empty__desc {
      margin: 0.5rem 0 0;
      max-width: 24rem;
      font-size: 0.875rem;
      line-height: 1.5;
      color: var(--text-muted);
    }

    .portal-empty__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      justify-content: center;
      margin-top: 1rem;
    }

    .portal-empty__actions:empty {
      display: none;
    }
  `,
})
export class AdeniPortalEmptyStateComponent {
  @HostBinding("attr.title") protected readonly nativeTitle: null = null;

  readonly title = input.required<string>();
  readonly description = input<string>("");
  readonly icon = input<AdeniCarbonIconName>("search");
  readonly compact = input(false, { transform: booleanAttribute });
  readonly embedded = input(false, { transform: booleanAttribute });

}
