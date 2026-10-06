import {
  Component,
  effect,
  ElementRef,
  HostListener,
  inject,
  input,
  signal,
  viewChild,
} from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";
import {
  discoverSearchToPath,
  getAskAdeniPrompts,
  resolveDiscoverySearch,
  t,
} from "@adeni/shared";
import { DiscoverLocaleService } from "../core/services/discover-locale.service";
import { MarketContextService } from "../core/services/market-context.service";

export type DiscoverySearchVariant = "hero" | "compact" | "default";

@Component({
  selector: "app-discovery-search",
  standalone: true,
  imports: [FormsModule],
  templateUrl: "./discovery-search.component.html",
  styleUrl: "./discovery-search.component.scss",
})
export class DiscoverySearchComponent {
  private readonly router = inject(Router);
  private readonly localeService = inject(DiscoverLocaleService);
  private readonly market = inject(MarketContextService);
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly variant = input<DiscoverySearchVariant>("default");
  readonly initialQuery = input("");

  readonly open = signal(false);
  readonly hint = signal<string | null>(null);
  query = "";

  private readonly inputEl = viewChild<ElementRef<HTMLInputElement>>("searchInput");

  constructor() {
    effect(() => {
      this.query = this.initialQuery();
    });
  }

  get locale() {
    return this.localeService.locale();
  }

  get placeholder(): string {
    return t(this.locale, "search.placeholder");
  }

  get prompts(): string[] {
    return getAskAdeniPrompts(this.locale);
  }

  get askTitle(): string {
    return t(this.locale, "search.askTitle");
  }

  get askDescription(): string {
    return t(this.locale, "search.askDescription");
  }

  get tryAsking(): string {
    return t(this.locale, "search.tryAsking");
  }

  get findServices(): string {
    return t(this.locale, "search.findServices");
  }

  get compactQueryLabel(): string {
    return this.query.trim() || t(this.locale, "search.searchOrAsk");
  }

  get compactLocationLabel(): string {
    return this.market.market()?.name ?? t(this.locale, "search.nearYou");
  }

  get compactCategoryLabel(): string {
    return t(this.locale, "search.anyCategory");
  }

  openPanel(): void {
    this.open.set(true);
    queueMicrotask(() => this.inputEl()?.nativeElement.focus());
  }

  closePanel(): void {
    this.open.set(false);
  }

  submit(event?: Event): void {
    event?.preventDefault();
    this.navigate(this.query);
  }

  navigate(text: string): void {
    const trimmed = text.trim();
    if (!trimmed) {
      void this.router.navigateByUrl("/discover");
      this.closePanel();
      return;
    }

    const params = resolveDiscoverySearch(trimmed);
    this.hint.set(params.summary ?? null);
    this.query = trimmed;
    this.closePanel();
    void this.router.navigateByUrl(discoverSearchToPath(params));
  }

  @HostListener("document:mousedown", ["$event"])
  onDocumentMouseDown(event: MouseEvent): void {
    if (!this.open()) {
      return;
    }
    const target = event.target as Node;
    if (!this.host.nativeElement.contains(target)) {
      this.closePanel();
    }
  }

  @HostListener("document:keydown.escape")
  onEscape(): void {
    if (this.open()) {
      this.closePanel();
    }
  }
}
