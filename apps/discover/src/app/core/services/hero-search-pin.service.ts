import { Injectable, OnDestroy, signal } from "@angular/core";

export const HERO_SEARCH_ANCHOR_ID = "adeni-hero-search-anchor";

const HEADER_OFFSET_PX = 72;

@Injectable({ providedIn: "root" })
export class HeroSearchPinService implements OnDestroy {
  readonly pinned = signal(false);

  private observer: IntersectionObserver | null = null;
  private retryId = 0;

  watch(enabled: boolean): void {
    this.teardown();
    if (!enabled || typeof window === "undefined" || typeof IntersectionObserver === "undefined") {
      this.pinned.set(false);
      return;
    }

    const attach = (target: HTMLElement) => {
      this.observer = new IntersectionObserver(
        ([entry]) => this.pinned.set(!entry.isIntersecting),
        {
          root: null,
          rootMargin: `-${HEADER_OFFSET_PX}px 0px 0px 0px`,
          threshold: 0,
        },
      );
      this.observer.observe(target);
    };

    const tryAttach = () => {
      const target = document.getElementById(HERO_SEARCH_ANCHOR_ID);
      if (!target) {
        return false;
      }
      attach(target);
      return true;
    };

    if (!tryAttach()) {
      this.retryId = window.setInterval(() => {
        if (tryAttach()) {
          window.clearInterval(this.retryId);
          this.retryId = 0;
        }
      }, 100);
    }
  }

  ngOnDestroy(): void {
    this.teardown();
  }

  private teardown(): void {
    if (this.retryId) {
      window.clearInterval(this.retryId);
      this.retryId = 0;
    }
    this.observer?.disconnect();
    this.observer = null;
    this.pinned.set(false);
  }
}
