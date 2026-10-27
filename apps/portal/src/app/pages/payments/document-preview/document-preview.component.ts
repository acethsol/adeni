import { Component, computed, effect, inject, input, output, untracked } from "@angular/core";
import { A11yModule } from "@angular/cdk/a11y";
import { DomSanitizer } from "@angular/platform-browser";
import { AdeniLocaleService, AdeniModalComponent } from "@adeni/ui";
import { customerInsightsCopy } from "@adeni/shared/customer-insights-copy";
import type { BillingDocumentResponse } from "@adeni/shared";
import { DocumentPreviewDataService } from "./document-preview-data.service";

@Component({
  selector: "app-document-preview", standalone: true, imports: [AdeniModalComponent, A11yModule], providers: [DocumentPreviewDataService],
  template: `<adeni-modal [open]="true" [title]="text('preview') + ' · ' + document().documentNumber" size="2xl" [hasFooter]="false" (closed)="closed.emit()" cdkTrapFocus [cdkTrapFocusAutoCapture]="true">
      <p>{{ document().kind }} · {{ document().status }}</p>
      @if (document().status === 'draft') { <p>{{ text('draft') }}</p> }
      <button type="button" cdkFocusInitial (click)="closed.emit()">{{ text('close') }}</button>
      @if (data.loading()) { <p role="status">{{ text('loading') }}</p> }
      @if (data.failed()) { <p role="alert">{{ text('error') }}</p> }
      @if (data.url(); as url) {
        <p><a [href]="url" target="_blank" rel="noopener noreferrer">{{ text('open') }}</a>
          <a [href]="url" [download]="document().kind + '-' + document().id + '.pdf'">{{ text('download') }}</a></p>
        <iframe [src]="viewerUrl()" [title]="text('preview')" referrerpolicy="no-referrer"></iframe>
      }
    </adeni-modal>`,
  styles: `iframe { width: 100%; height: 55vh; border: 1px solid var(--border); } a, button { margin-inline-end: 1rem; }`,
})
export class DocumentPreviewComponent {
  readonly document = input.required<BillingDocumentResponse>(); readonly locationId = input<string>(); readonly closed = output<void>();
  readonly data = inject(DocumentPreviewDataService); private readonly sanitizer = inject(DomSanitizer); private readonly locale = inject(AdeniLocaleService);
  readonly text = (key: Parameters<typeof customerInsightsCopy>[1]) => customerInsightsCopy(this.locale.locale(), key);
  // The value is never an API/public URL or user input; this service creates/revokes it from a validated private PDF.
  readonly viewerUrl = computed(() => { const url = this.data.url(); return url ? this.sanitizer.bypassSecurityTrustResourceUrl(url) : null; });
  constructor() {
    effect(cleanup => {
      const id = this.document().id; const location = this.locationId();
      // Loading reads/revokes the current Blob; those UI signals must not restart the request effect.
      untracked(() => { void this.data.load(id, location); });
      cleanup(() => untracked(() => this.data.clear()));
    });
  }
}
