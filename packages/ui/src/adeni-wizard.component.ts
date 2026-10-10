import { Component, computed, input, output } from "@angular/core";

/** One beat in a multi-step flow. `label` is the rail; `title` and `lede` are the stage. */
export type AdeniWizardStep = {
  id: string;
  label: string;
  title: string;
  lede?: string | null;
};

/**
 * Shared step chrome for portal and discover wizards.
 * Project the step body as default content, actions with `wizardActions`,
 * and an optional side card with `wizardPreview` when `showPreview` is set.
 */
@Component({
  selector: "adeni-wizard",
  standalone: true,
  template: `
    <ol class="rail" [attr.aria-label]="ariaLabel()">
      @for (step of steps(); track step.id; let i = $index) {
        <li [class.on]="i === index()" [class.done]="i < index()">
          @if (allowJump() && i < index()) {
            <button type="button" class="beat" (click)="choose(step.id)">
              {{ step.label }}
            </button>
          } @else {
            <span class="beat" [attr.aria-current]="i === index() ? 'step' : null">
              {{ step.label }}
            </span>
          }
        </li>
      }
    </ol>

    <div class="stage" [class.split]="showPreview()">
      <div class="main">
        @if (active(); as step) {
          <header class="head">
            <h3>{{ step.title }}</h3>
            @if (step.lede) {
              <p class="lede">{{ step.lede }}</p>
            }
          </header>
        }
        <ng-content />
      </div>
      @if (showPreview()) {
        <ng-content select="[wizardPreview]" />
      }
    </div>
    <div class="foot">
      <ng-content select="[wizardActions]" />
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .rail {
      display: flex;
      flex-wrap: wrap;
      gap: 0.15rem 1.25rem;
      list-style: none;
      margin: 0 0 1.25rem;
      padding: 0 0 0.75rem;
      border-bottom: 1px solid var(--border, #e0e0e0);
    }

    .rail li {
      margin: 0;
      padding: 0;
    }

    .beat {
      display: inline-block;
      padding: 0 0 0.75rem;
      margin-bottom: -0.75rem;
      border: 0;
      border-bottom: 2px solid transparent;
      background: transparent;
      color: var(--text-muted, #6f6f6f);
      font: inherit;
      font-size: 0.82rem;
      font-weight: 600;
      line-height: 1.2;
    }

    button.beat {
      cursor: pointer;
    }

    button.beat:hover {
      color: var(--text, #161616);
    }

    li.on .beat {
      border-bottom-color: var(--adeni-purple, #7f56ff);
      color: var(--text, #161616);
    }

    li.done .beat {
      color: var(--text, #161616);
    }

    .stage.split {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 16rem;
      gap: 1.25rem;
      align-items: start;
    }

    .head {
      margin: 0 0 1rem;
    }

    h3 {
      margin: 0;
      font-size: 1.15rem;
      font-weight: 650;
      letter-spacing: -0.01em;
    }

    .lede {
      margin: 0.3rem 0 0;
      max-width: 36rem;
      color: var(--text-muted, #6f6f6f);
      font-size: 0.88rem;
      line-height: 1.4;
    }

    .foot {
      margin-top: 1.1rem;
    }

    .foot:not(:has(*)) {
      display: none;
      margin: 0;
    }

    @media (max-width: 860px) {
      .stage.split {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class AdeniWizardComponent {
  readonly steps = input.required<readonly AdeniWizardStep[]>();
  readonly activeId = input.required<string>();
  readonly ariaLabel = input("Steps");
  /** Completed steps become buttons that emit `stepSelect`. */
  readonly allowJump = input(false);
  readonly showPreview = input(false);

  readonly stepSelect = output<string>();

  readonly index = computed(() => {
    const found = this.steps().findIndex((step) => step.id === this.activeId());
    return found >= 0 ? found : 0;
  });

  readonly active = computed(() => this.steps()[this.index()] ?? null);

  choose(id: string): void {
    this.stepSelect.emit(id);
  }
}
