import { Component, inject, input } from "@angular/core";
import type { CustomerSegments } from "@adeni/shared/customer-segments";
import { customerInsightsCopy, customerSegmentReason } from "@adeni/shared/customer-insights-copy";
import { AdeniLocaleService } from "@adeni/ui";
@Component({
  selector: "app-customer-segment-chips", standalone: true,
  template: `@if (value(); as row) {
    <details><summary>{{ text('title') }}: @for (match of row.matches; track match.id) { <span class="chip">{{ text(match.id) }}</span> }
      @if (!row.matches.length) { {{ text(row.availability) }} }
    </summary>
    <p>{{ row.locationId ? text('scope') : text('business') }} · {{ row.timeZoneId }} · {{ text('evaluated') }} {{ row.asOf }}</p>
    @if (row.approximateVisitDates) { <p>{{ text('approximate') }}</p> }
    @for (match of row.matches; track match.id) { <p>{{ text(match.id) }} — {{ reason(match.id, match.count, match.daysSinceLastVisit) }}</p> }
    <p>{{ text('definitions') }}</p></details>
  }`,
  styles: `.chip { display: inline-block; padding: .1rem .4rem; margin: .1rem; border: 1px solid var(--border); border-radius: .5rem; font-size: .8rem; } summary { cursor: pointer; }`,
})
export class CustomerSegmentChipsComponent {
  readonly value = input<CustomerSegments>();
  private readonly locale = inject(AdeniLocaleService);
  readonly text = (key: Parameters<typeof customerInsightsCopy>[1]) => customerInsightsCopy(this.locale.locale(), key);
  readonly reason = (id: Parameters<typeof customerSegmentReason>[1], count: number, days: number | null) => customerSegmentReason(this.locale.locale(), id, count, days);
}
