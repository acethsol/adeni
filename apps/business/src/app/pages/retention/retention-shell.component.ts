import { Component, DestroyRef, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { NavigationStart, Router, RouterOutlet } from "@angular/router";
import { AdeniFeedbackService, injectAdeniTranslate } from "@adeni/ui";
import { filter } from "rxjs";
import { RetentionNavTabsComponent } from "./retention-nav-tabs.component";

const RETENTION_TAB_PATH = /^\/retention\/(packages|memberships|loyalty)\/?$/;

function retentionTabPath(url: string): string | null {
  const path = url.split("?")[0].split("#")[0];
  return RETENTION_TAB_PATH.test(path) ? path : null;
}

/** Shared chrome for Packages · Memberships · Loyalty (tabs + child route). */
@Component({
  selector: "app-retention-shell",
  standalone: true,
  imports: [RouterOutlet, RetentionNavTabsComponent],
  template: `
    <app-retention-nav-tabs />
    <router-outlet />
  `,
})
export class RetentionShellComponent {
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly tr = injectAdeniTranslate();
  /** Path only (no query) — loyalty `?loyaltyTab=` must not trigger the global loader. */
  private lastRetentionTabPath: string | null = null;

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationStart => event instanceof NavigationStart),
        takeUntilDestroyed(),
      )
      .subscribe((event) => {
        const path = retentionTabPath(event.url);
        if (!path) {
          this.lastRetentionTabPath = null;
          return;
        }
        if (this.lastRetentionTabPath === path) {
          return;
        }
        this.lastRetentionTabPath = path;
        this.feedback.showLoading(
          this.tr("toast.loading"),
          this.tr("businessApp.retention.loadingSection"),
        );
      });
  }
}
