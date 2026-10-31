import { Injectable, signal } from "@angular/core";
import type { AdeniBreadcrumb } from "@adeni/shared";

/**
 * Lets a detail page append dynamic breadcrumb segments (e.g. customer or business name).
 * App shells clear this on {@link NavigationStart} so stale labels do not leak across routes.
 */
@Injectable({ providedIn: "root" })
export class AdeniPageBreadcrumbService {
  private readonly _extras = signal<readonly AdeniBreadcrumb[]>([]);

  readonly extras = this._extras.asReadonly();

  setExtras(crumbs: readonly AdeniBreadcrumb[]): void {
    this._extras.set(crumbs);
  }

  clearExtras(): void {
    this._extras.set([]);
  }
}
