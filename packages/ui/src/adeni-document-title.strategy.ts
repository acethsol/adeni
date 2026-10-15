import { Inject, Injectable, InjectionToken, Injector, Provider } from "@angular/core";
import { Title } from "@angular/platform-browser";
import { Router, RouterStateSnapshot, TitleStrategy } from "@angular/router";

export const ADENI_DEFAULT_APP_TITLE = new InjectionToken<string>("ADENI_DEFAULT_APP_TITLE");

/**
 * Sets `Page · Brand` document titles from route `title` data.
 * Call {@link setAppTitle} after load to personalize Brand (e.g. business name).
 */
@Injectable()
export class AdeniDocumentTitleStrategy extends TitleStrategy {
  private appTitle: string;
  private readonly fallbackTitle: string;

  constructor(
    private readonly documentTitle: Title,
    private readonly injector: Injector,
    @Inject(ADENI_DEFAULT_APP_TITLE) defaultAppTitle: string,
  ) {
    super();
    this.fallbackTitle = defaultAppTitle.trim() || "Adeni";
    this.appTitle = this.fallbackTitle;
  }

  /** Brand segment after the page title (business name, product name, …). */
  setAppTitle(appTitle: string): void {
    const next = appTitle.trim() || this.fallbackTitle;
    if (next === this.appTitle) {
      return;
    }
    this.appTitle = next;
    // Lazy Router lookup — constructor inject of Router cycles with TitleStrategy.
    const router = this.injector.get(Router);
    this.updateTitle(router.routerState.snapshot);
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const page = this.buildTitle(snapshot);
    this.documentTitle.setTitle(page ? `${page} · ${this.appTitle}` : this.appTitle);
  }
}

export function provideAdeniDocumentTitle(appTitle: string): Provider[] {
  return [
    { provide: ADENI_DEFAULT_APP_TITLE, useValue: appTitle },
    AdeniDocumentTitleStrategy,
    { provide: TitleStrategy, useExisting: AdeniDocumentTitleStrategy },
  ];
}
