import { Component, DestroyRef, PLATFORM_ID, inject, signal } from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { distinctUntilChanged, map } from "rxjs";
import { IntakeFieldsComponent, injectAdeniApiError, injectAdeniTranslate } from "@adeni/ui";
import type { IntakeAnswer, IntakeStatus, IntakeView } from "@adeni/shared";
import { CustomerApiService } from "../../core/services/customer-api.service";

@Component({ selector: "app-intake", standalone: true, imports: [RouterLink, FormsModule, IntakeFieldsComponent],
  styles: `:host { display:block; max-width:48rem; margin:auto; padding:1.5rem; } nav a { display:block; padding:.5rem; } button { padding:.5rem 1rem; }`,
  template: `
    <h1>{{tr('intake.title')}}</h1><a routerLink="/my-bookings">{{tr('intake.myBookings')}}</a>
    <p>{{tr('intake.nonClinical')}}</p>
    @if(error()) { <p role="alert">{{error()}}</p> }
    @if(loading()) { <p role="status">{{tr('intake.loading')}}</p> }
    <nav>@for(status of statuses(); track status.id) { <a [routerLink]="['/my-bookings',bookingId(),'intake',status.id]">{{tr('intake.form')}} · {{tr('intake.states.'+status.state)}}</a> } @empty { @if(!loading()) { <p>{{tr('intake.notRequired')}}</p> } }</nav>
    @if(view(); as v) {
      <h2>{{v.definition.title}}</h2><p>{{v.definition.description}}</p><p>{{v.definition.instructions}}</p>
      @if(v.assignment.canSubmit) {
        <p>{{tr('intake.finalNotice')}}</p>
        <form ngNativeValidate (ngSubmit)="submit()"><fieldset [disabled]="submitting()">
          <adeni-intake-fields [definition]="v.definition" [answers]="answers()" (answersChange)="answers.set($event)" />
          <button type="submit">{{tr('intake.submit')}}</button>
        </fieldset></form>
      } @else {
        <p>{{tr('intake.states.'+v.assignment.state)}}</p>
        @if(v.answers) { <adeni-intake-fields [definition]="v.definition" [answers]="v.answers" [readOnly]="true" /> }
      }
    }
  `,
})
export class IntakeComponent {
  private readonly api = inject(CustomerApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  readonly tr = injectAdeniTranslate(); readonly apiErr = injectAdeniApiError();
  readonly bookingId = signal(""); readonly statuses = signal<IntakeStatus[]>([]); readonly view = signal<IntakeView | null>(null);
  readonly answers = signal<IntakeAnswer[]>([]); readonly error = signal<string | null>(null); readonly loading = signal(false); readonly submitting = signal(false);
  private abort: AbortController | null = null; private assignmentId = ""; private key = "";
  constructor() {
    this.destroy.onDestroy(() => { this.abort?.abort(); this.answers.set([]); this.view.set(null); });
    if (isPlatformBrowser(inject(PLATFORM_ID))) this.route.paramMap.pipe(map(p => [p.get('bookingId') ?? '', p.get('assignmentId') ?? ''] as const),
      distinctUntilChanged((a,b) => a[0] === b[0] && a[1] === b[1]), takeUntilDestroyed(this.destroy)).subscribe(([booking,assignment]) => { void this.load(booking,assignment); });
  }
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
