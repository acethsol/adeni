import { Injectable, Provider } from "@angular/core";
import { Title } from "@angular/platform-browser";
import { RouterStateSnapshot, TitleStrategy } from "@angular/router";

/** Sets `Page · App` document titles from route `title` data. */
@Injectable()
export class AdeniDocumentTitleStrategy extends TitleStrategy {
  constructor(
    private readonly documentTitle: Title,
    private readonly appTitle: string,
  ) {
    super();
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const page = this.buildTitle(snapshot);
    this.documentTitle.setTitle(page ? `${page} · ${this.appTitle}` : this.appTitle);
  }
}

export function provideAdeniDocumentTitle(appTitle: string): Provider {
  return {
    provide: TitleStrategy,
    useFactory: (documentTitle: Title) =>
      new AdeniDocumentTitleStrategy(documentTitle, appTitle),
    deps: [Title],
  };
}
