import { Component, DestroyRef, PLATFORM_ID, inject, signal } from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { distinctUntilChanged, map } from "rxjs";
import {
  AdeniCarbonIconComponent,
  AdeniLocaleService,
  IntakeFieldsComponent,
  PortalPageComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
} from "@adeni/ui";
import { formatSlotTime, type IntakeAnswer, type IntakeStatus, type IntakeView } from "@adeni/shared";
import { CustomerApiService } from "../../core/services/customer-api.service";

@Component({
  selector: "app-intake",
  standalone: true,
  imports: [RouterLink, FormsModule, IntakeFieldsComponent, AdeniCarbonIconComponent, PortalPageComponent],
  templateUrl: "./intake.component.html",
  styleUrl: "./intake.component.scss",
})
export class IntakeComponent {
  private readonly api = inject(CustomerApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  readonly tr = injectAdeniTranslate(); readonly apiErr = injectAdeniApiError(); private readonly locale = inject(AdeniLocaleService).locale;
  readonly bookingId = signal(""); readonly statuses = signal<IntakeStatus[]>([]); readonly view = signal<IntakeView | null>(null);
  readonly answers = signal<IntakeAnswer[]>([]); readonly error = signal<string | null>(null); readonly loading = signal(false); readonly submitting = signal(false);
  private abort: AbortController | null = null; private assignmentId = ""; private key = "";
  constructor() {
    this.destroy.onDestroy(() => { this.abort?.abort(); this.answers.set([]); this.view.set(null); });
    if (isPlatformBrowser(inject(PLATFORM_ID))) this.route.paramMap.pipe(map(p => [p.get('bookingId') ?? '', p.get('assignmentId') ?? ''] as const),
      distinctUntilChanged((a,b) => a[0] === b[0] && a[1] === b[1]), takeUntilDestroyed(this.destroy)).subscribe(([booking,assignment]) => { void this.load(booking,assignment); });
  }
  when(iso: string): string { return formatSlotTime(iso, this.locale()); }
  async load(booking: string, assignment: string): Promise<void> {
    this.abort?.abort(); const abort = new AbortController(); this.abort = abort;
    this.bookingId.set(booking); this.assignmentId = assignment; this.key = crypto.randomUUID(); this.answers.set([]); this.view.set(null); this.statuses.set([]); this.error.set(null); this.loading.set(true); this.submitting.set(false);
    try {
      const result = await this.api.withAuthorizedClient(async c => ({ statuses: await c.intake.customerStatuses(booking, abort.signal),
        view: assignment ? await c.intake.customerRead(booking, assignment, abort.signal) : null }));
      if (!abort.signal.aborted) { this.statuses.set(result.statuses); this.view.set(result.view); }
    } catch(e) { if(!abort.signal.aborted) this.error.set(this.apiErr(e,'intake.loadError')); }
    finally { if(this.abort === abort) this.loading.set(false); }
  }
  async submit(): Promise<void> {
    const view = this.view(); const abort = this.abort;
    if(!view?.assignment.canSubmit || !abort || abort.signal.aborted || this.submitting()) return;
    this.submitting.set(true); this.error.set(null);
    try {
      const status = await this.api.withAuthorizedClient(c => c.intake.submit(this.bookingId(), this.assignmentId, view.assignment.versionId, this.answers(), this.key, abort.signal));
      if(!abort.signal.aborted) {
        this.answers.set([]);
        this.view.set({ ...view, assignment:status, answers:null });
        this.statuses.update(rows => rows.map(row => row.id === status.id ? status : row));
      }
    } catch(e) { if(!abort.signal.aborted) this.error.set(this.apiErr(e,'intake.submitError')); }
    finally { if(this.abort === abort) this.submitting.set(false); }
  }
}
