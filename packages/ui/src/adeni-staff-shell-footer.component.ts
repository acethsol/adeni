import { Component, computed, input } from "@angular/core";
import { ADENI_STAFF_APP_VERSION } from "@adeni/shared";
import { AdeniStaffAppContextComponent, type AdeniStaffAppContext } from "./adeni-staff-app-context.component";
import { injectAdeniTranslate } from "./locale/adeni-i18n";

/** Portal/admin shell footer: one slim row — legal, build, app chip. */
@Component({
  selector: "adeni-staff-shell-footer",
  standalone: true,
  imports: [AdeniStaffAppContextComponent],
  template: `
    <footer class="staff-shell-footer" [attr.data-app]="app()">
      <div class="staff-shell-footer__hairline" aria-hidden="true"></div>
      <div class="staff-shell-footer__row">
        <p class="staff-shell-footer__line">
          <span>{{ copyright() }}</span>
          <span class="staff-shell-footer__sep" aria-hidden="true"></span>
          <span class="staff-shell-footer__build" [attr.title]="buildHint()">v{{ version() }}</span>
          @if (devMode()) {
            <span class="staff-shell-footer__sep" aria-hidden="true"></span>
            <span class="staff-shell-footer__dev">{{ tr("common.staffFooter.localDev") }}</span>
          }
        </p>
        <adeni-staff-app-context
          [app]="app()"
          [label]="badgeLabel()"
          [attr.title]="captionLabel()"
          [compact]="true"
        />
      </div>
    </footer>
  `,
  styles: `
    .staff-shell-footer {
      flex-shrink: 0;
      padding: 0.45rem 0 0;
      color: var(--text-muted);
    }

    .staff-shell-footer__hairline {
      height: 1px;
      margin-bottom: 0.5rem;
      background: var(--border);
    }

    .staff-shell-footer[data-app="business"] .staff-shell-footer__hairline,
    .staff-shell-footer[data-app="portal"] .staff-shell-footer__hairline,
    .staff-shell-footer[data-app="consumer"] .staff-shell-footer__hairline {
      background: linear-gradient(
        90deg,
        color-mix(in srgb, var(--adeni-purple, #7f56ff) 28%, var(--border)) 0%,
        var(--border) 38%,
        transparent 100%
      );
    }

    .staff-shell-footer[data-app="admin"] .staff-shell-footer__hairline {
      background: linear-gradient(
        90deg,
        color-mix(in srgb, var(--adeni-teal, #13c2a3) 28%, var(--border)) 0%,
        var(--border) 38%,
        transparent 100%
      );
    }

    .staff-shell-footer__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem 0.75rem;
    }

    .staff-shell-footer__line {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.45rem;
      margin: 0;
      min-width: 0;
      font-size: 0.6875rem;
      font-weight: 500;
      line-height: 1.25;
    }

    .staff-shell-footer__sep {
      width: 3px;
      height: 3px;
      border-radius: 50%;
      background: color-mix(in srgb, var(--text-muted) 55%, transparent);
      flex-shrink: 0;
    }

    .staff-shell-footer[data-app="business"] .staff-shell-footer__sep,
    .staff-shell-footer[data-app="portal"] .staff-shell-footer__sep {
      background: color-mix(in srgb, var(--adeni-purple, #7f56ff) 45%, var(--text-muted));
    }

    .staff-shell-footer[data-app="admin"] .staff-shell-footer__sep {
      background: color-mix(in srgb, var(--adeni-teal, #13c2a3) 45%, var(--text-muted));
    }

    .staff-shell-footer__build {
      font-variant-numeric: tabular-nums;
      font-weight: 600;
      font-family: ui-monospace, "Cascadia Mono", "Segoe UI Mono", monospace;
      font-size: 0.625rem;
      letter-spacing: 0.02em;
      color: var(--text);
      opacity: 0.72;
      cursor: default;
    }

    .staff-shell-footer__dev {
      font-size: 0.625rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      opacity: 0.85;
    }
  `,
})
export class AdeniStaffShellFooterComponent {
  protected readonly tr = injectAdeniTranslate();

  readonly app = input.required<AdeniStaffAppContext>();
  readonly badgeLabel = input.required<string>();
  readonly captionLabel = input.required<string>();
  readonly version = input(ADENI_STAFF_APP_VERSION);
  readonly devMode = input(false);

  readonly copyright = computed(() =>
    this.tr("common.staffFooter.copyright", { year: String(new Date().getFullYear()) }),
  );

  readonly buildHint = computed(() =>
    this.tr("common.staffFooter.buildHint", {
      version: this.version(),
      app: this.captionLabel(),
    }),
  );
}
