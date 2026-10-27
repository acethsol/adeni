import { isPlatformBrowser } from "@angular/common";
import { afterNextRender, DestroyRef, Directive, effect, inject, PLATFORM_ID, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute } from "@angular/router";
import { parseBookingPreselection } from "@adeni/shared";
import { injectAdeniTranslate } from "@adeni/ui";
import { CustomerApiService } from "../core/services/customer-api.service";
import { BookingPanelComponent } from "./booking-panel.component";

/** Browser-only enhancement: profile SSR, TransferState and canonical SEO remain independent of selectors. */
@Directive({ selector: "app-booking-panel[adeniBookingPreselection]", standalone: true })
export class BookingPreselectionDirective {
  constructor() {
    const panel = inject(BookingPanelComponent); const api = inject(CustomerApiService); const tr = injectAdeniTranslate();
    const route = inject(ActivatedRoute); const browser = isPlatformBrowser(inject(PLATFORM_ID));
    const params = toSignal(route.queryParamMap); const ready = signal(false);
    afterNextRender(() => ready.set(true));
    let hadContext = false;
    effect(cleanup => {
      if (!browser || !ready()) return;
      const query = params(); const slug = panel.slug(); const services = panel.services();
      if (!query || !slug || !services.length) return;
      const hasContext = query.has("service") || query.has("practitioner");
      if (!hasContext && !hadContext) return;
      panel.resetPreselection(); hadContext = hasContext;
      if (!hasContext) return;
      const abort = new AbortController(); cleanup(() => abort.abort());
      const raw = new URLSearchParams(); for (const key of query.keys) for (const value of query.getAll(key)) raw.append(key,value);
      try {
        const selection = parseBookingPreselection(raw);
        void api.createPublicClient().calendar.bookingContext(slug, selection, abort.signal).then(validated => {
          if (!abort.signal.aborted) panel.applyPreselection(validated.serviceId, validated.practitionerId);
        }).catch(() => { if (!abort.signal.aborted) panel.error.set(tr("calendarUi.invalidSelection")); });
      } catch { panel.error.set(tr("calendarUi.invalidSelection")); }
    });
    inject(DestroyRef).onDestroy(() => panel.cancelSelectionRequests());
  }
}
