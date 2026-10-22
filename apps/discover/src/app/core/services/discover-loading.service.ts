import { Injectable, inject } from "@angular/core";
import { AdeniFeedbackService, injectAdeniTranslate } from "@adeni/ui";

/** @deprecated Prefer AdeniFeedbackService directly — kept as a thin alias for discover pages. */
@Injectable({ providedIn: "root" })
export class DiscoverLoadingService {
  protected readonly tr = injectAdeniTranslate();
  private readonly feedback = inject(AdeniFeedbackService);

  show(label = this.tr("common.loading"), detail = this.tr("discover.core.justMoment")): void {
    this.feedback.showLoading(label, detail);
  }

  hide(): void {
    this.feedback.hideLoading();
  }

  async run<T>(work: () => Promise<T>, label?: string, detail?: string): Promise<T> {
    return this.feedback.runLoading(work, label, detail);
  }
}
