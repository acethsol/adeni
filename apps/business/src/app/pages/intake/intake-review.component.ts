import { Component, DestroyRef, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { distinctUntilChanged, map } from "rxjs";
import {
  AdeniCarbonIconComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  IntakeFieldsComponent,
  PortalPageComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
  AdeniPageBreadcrumbService,
} from "@adeni/ui";
import type { IntakeStatus, IntakeView } from "@adeni/shared";
import {
  ADENI_BREADCRUMB_EXTRAS_STATE,
  readBreadcrumbExtrasFromHistory,
} from "../../core/business-breadcrumb-navigation";
import { BusinessApiService } from "../../core/services/business-api.service";
import { BusinessSessionService } from "../../core/services/business-session.service";
import { intakePillTone } from "./intake-booking-display";

@Component({
  selector: "app-intake-review",
  standalone: true,
  imports: [
    RouterLink,
    PortalPageComponent,
    IntakeFieldsComponent,
    AdeniCarbonIconComponent,
    AdeniPillComponent,
    AdeniPortalEmptyStateComponent,
  ],
  templateUrl: "./intake-review.component.html",
  styleUrl: "./intake-review.component.scss",
})
export class IntakeReviewComponent implements OnInit {
  readonly tr = injectAdeniTranslate();
  readonly apiErr = injectAdeniApiError();
  readonly intakePillTone = intakePillTone;
  private readonly api = inject(BusinessApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly pageBreadcrumbs = inject(AdeniPageBreadcrumbService);
  private readonly destroy = inject(DestroyRef);
  readonly session = inject(BusinessSessionService);

  readonly statuses = signal<IntakeStatus[]>([]);
  readonly view = signal<IntakeView | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  private booking = "";
  private abort = new AbortController();
  private request = 0;

  canReview(): boolean {
    return this.session.can("business.bookings")
      && ["owner", "manager"].includes(this.session.permissionRole());
  }

  constructor() {
    this.destroy.onDestroy(() => {
      this.abort.abort();
      this.view.set(null);
      this.pageBreadcrumbs.clearExtras();
    });
  }

  ngOnInit(): void {
    this.applyBreadcrumbExtrasFromNavigation();
    this.route.paramMap.pipe(
      map((p) => p.get("id") ?? ""),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroy),
    ).subscribe((id) => {
      this.applyBreadcrumbExtrasFromNavigation();
      void this.load(id);
    });
  }

  private applyBreadcrumbExtrasFromNavigation(): void {
    const fromHistory = readBreadcrumbExtrasFromHistory();
    if (fromHistory) {
      this.pageBreadcrumbs.setExtras(fromHistory);
      return;
    }
    const nav = this.router.currentNavigation();
    const fromNav = nav?.extras?.state?.[ADENI_BREADCRUMB_EXTRAS_STATE];
    if (Array.isArray(fromNav) && fromNav.length > 0) {
      this.pageBreadcrumbs.setExtras(fromNav as { label: string; path?: string }[]);
    }
  }

  async load(id: string): Promise<void> {
    this.abort.abort();
    const abort = new AbortController();
    this.abort = abort;
    this.booking = id;
    this.request++;
    this.view.set(null);
    this.statuses.set([]);
    this.error.set(null);
    this.loading.set(true);
    try {
      await this.session.ensureLoaded();
      const rows = await this.api.withAuthorizedClient((c) => c.intake.staffStatuses(id, abort.signal));
      if (!abort.signal.aborted) {
        this.statuses.set(rows);
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        this.error.set(this.apiErr(e, "intake.loadError"));
      }
    } finally {
      if (this.abort === abort) {
        this.loading.set(false);
      }
    }
  }

  async review(id: string): Promise<void> {
    const request = ++this.request;
    const abort = this.abort;
    this.view.set(null);
    this.error.set(null);
    try {
      const view = await this.api.withAuthorizedClient((c) => c.intake.review(this.booking, id, abort.signal));
      if (!abort.signal.aborted && request === this.request) {
        this.view.set(view);
      }
    } catch (e) {
      if (!abort.signal.aborted && request === this.request) {
        this.error.set(this.apiErr(e, "intake.loadError"));
      }
    }
  }
}
