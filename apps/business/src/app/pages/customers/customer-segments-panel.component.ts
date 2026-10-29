import { Component, effect, inject, input } from "@angular/core";
import { customerInsightsCopy } from "@adeni/shared/customer-insights-copy";
import { AdeniLocaleService } from "@adeni/ui";
import { CustomerSegmentsDataService } from "./customer-segments-data.service";
import { CustomerSegmentChipsComponent } from "./customer-segment-chips.component";
@Component({ selector: "app-customer-segments-panel", standalone: true, imports: [CustomerSegmentChipsComponent], providers: [CustomerSegmentsDataService],
  template: `@if (data.loading()) { <p role="status">{{ text('loading') }}</p> }
    @if (data.failed()) { <p role="status">{{ text('error') }}</p> }
    <app-customer-segment-chips [value]="data.rows().get(customerId())" />`,
})
export class CustomerSegmentsPanelComponent {
  readonly customerId = input.required<string>(); readonly locationId = input<string>();
  readonly data = inject(CustomerSegmentsDataService); private readonly locale = inject(AdeniLocaleService);
  readonly text = (key: Parameters<typeof customerInsightsCopy>[1]) => customerInsightsCopy(this.locale.locale(), key);
  constructor() { effect(cleanup => { void this.data.load([this.customerId()], this.locationId()); cleanup(() => this.data.cancel()); }); }
}
