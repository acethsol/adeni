import { Component, input } from "@angular/core";

export type AdeniStaffAppContext = "business" | "admin";

/** Compact “which app am I in?” marker for staff shells (business vs admin). */
@Component({
  selector: "adeni-staff-app-context",
  standalone: true,
  template: `
    <span class="staff-app-context" [class.staff-app-context--compact]="compact()" [attr.data-app]="app()">
      <span class="staff-app-context__mark" aria-hidden="true"></span>
      <span class="staff-app-context__label">{{ label() }}</span>
    </span>
  `,
  styles: `
    .staff-app-context {
      display: inline-flex;
      align-items: center;
      gap: 0.45rem;
      flex-shrink: 0;
      min-height: 1.625rem;
      padding: 0 0.55rem 0 0.4rem;
      border: 1px solid var(--border);
      border-radius: 999px;
      background: color-mix(in srgb, var(--text) 4%, var(--surface));
      color: var(--text-muted);
      font-size: 0.6875rem;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      line-height: 1;
    }

    .staff-app-context__mark {
      width: 0.2rem;
      height: 0.85rem;
      border-radius: 999px;
      background: var(--adeni-purple, #7f56ff);
    }

    .staff-app-context[data-app="admin"] .staff-app-context__mark {
      background: var(--adeni-teal, #13c2a3);
    }

    .staff-app-context[data-app="business"],
    .staff-app-context[data-app="portal"] {
      border-color: color-mix(in srgb, var(--adeni-purple, #7f56ff) 22%, var(--border));
      background: color-mix(in srgb, var(--adeni-purple, #7f56ff) 7%, var(--surface));
    }

    .staff-app-context[data-app="admin"] {
      border-color: color-mix(in srgb, var(--adeni-teal, #13c2a3) 22%, var(--border));
      background: color-mix(in srgb, var(--adeni-teal, #13c2a3) 7%, var(--surface));
    }

    .staff-app-context__label {
      color: var(--text);
      opacity: 0.92;
    }

    .staff-app-context--compact {
      min-height: 1.25rem;
      gap: 0.35rem;
      padding: 0 0.4rem 0 0.3rem;
      font-size: 0.625rem;
    }

    .staff-app-context--compact .staff-app-context__mark {
      width: 0.15rem;
      height: 0.65rem;
    }
  `,
})
export class AdeniStaffAppContextComponent {
  readonly app = input.required<AdeniStaffAppContext>();
  readonly label = input.required<string>();
  readonly compact = input(false);
}
