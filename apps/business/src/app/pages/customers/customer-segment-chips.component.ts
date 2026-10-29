import { Component, inject, input } from "@angular/core";
import type { CustomerSegmentId, CustomerSegments } from "@adeni/shared/customer-segments";
import { formatSlotTime } from "@adeni/shared";
import { customerInsightsCopy, customerSegmentReason } from "@adeni/shared/customer-insights-copy";
import { AdeniLocaleService, AdeniPillComponent, type AdeniPillTone } from "@adeni/ui";

@Component({
  selector: "app-customer-segment-chips",
  standalone: true,
  imports: [AdeniPillComponent],
  templateUrl: "./customer-segment-chips.component.html",
  styleUrl: "./customer-segment-chips.component.scss",
})
export class CustomerSegmentChipsComponent {
  readonly value = input<CustomerSegments>();
  private readonly locale = inject(AdeniLocaleService);
  readonly text = (key: Parameters<typeof customerInsightsCopy>[1]) =>
    customerInsightsCopy(this.locale.locale(), key);
  readonly reason = (id: CustomerSegmentId, count: number, days: number | null) =>
    customerSegmentReason(this.locale.locale(), id, count, days);
  readonly formatEvaluated = (iso: string): string => formatSlotTime(iso, this.locale.locale());

  pillTone(id: CustomerSegmentId): AdeniPillTone {
    switch (id) {
      case "vip":
      case "recentlyActive":
      case "newCustomer":
        return "success";
      case "atRisk":
      case "frequentQueueNoShow":
        return "warning";
      case "lapsed":
        return "neutral";
      default:
        return "info";
    }
  }
}
