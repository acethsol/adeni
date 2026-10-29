import { Component, DestroyRef, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { distinctUntilChanged, map } from "rxjs";
import { IntakeFieldsComponent, PortalPageComponent, injectAdeniApiError, injectAdeniTranslate } from "@adeni/ui";
import type { IntakeStatus, IntakeView } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";
import { BusinessSessionService } from "../../core/services/business-session.service";

@Component({selector:'app-intake-review',standalone:true,imports:[RouterLink,PortalPageComponent,IntakeFieldsComponent],
 template:`<app-portal-page [title]="tr('intake.status')" [description]="tr('intake.nonClinical')">
 <a pageActions routerLink="/bookings">{{tr('common.bookings')}}</a>
 @if(error()){<p role="alert">{{error()}}</p>}
 @if(loading()){<p role="status">{{tr('intake.loading')}}</p>}
 <ul>@for(row of statuses();track row.id){<li>{{tr('intake.states.'+row.state)}} @if(row.submittedAt){<time [attr.datetime]="row.submittedAt">{{row.submittedAt}}</time>}
 @if(canReview()){<button type="button" (click)="review(row.id)">{{tr('intake.review')}}</button>}</li>}@empty{@if(!loading()){<li>{{tr('intake.notRequired')}}</li>}}</ul>
 @if(!canReview()){<p>{{tr('intake.noAccess')}}</p>}
 @if(view();as v){<h2>{{v.definition.title}}</h2><p>{{v.definition.instructions}}</p>
 @if(v.answers){<adeni-intake-fields [definition]="v.definition" [answers]="v.answers" [readOnly]="true" />}@else{<p>{{tr('intake.unavailable')}}</p>}}
 </app-portal-page>`})
export class IntakeReviewComponent implements OnInit{
 readonly tr=injectAdeniTranslate();readonly apiErr=injectAdeniApiError();private readonly api=inject(BusinessApiService);private readonly route=inject(ActivatedRoute);private readonly destroy=inject(DestroyRef);readonly session=inject(BusinessSessionService);
 readonly statuses=signal<IntakeStatus[]>([]);readonly view=signal<IntakeView|null>(null);readonly error=signal<string|null>(null);readonly loading=signal(false);
 private booking='';private abort=new AbortController();private request=0;
 canReview():boolean{return this.session.can('business.bookings')&&['owner','manager'].includes(this.session.permissionRole());}
 constructor(){this.destroy.onDestroy(()=>{this.abort.abort();this.view.set(null);});}
 ngOnInit():void{this.route.paramMap.pipe(map(p=>p.get('id')??''),distinctUntilChanged(),takeUntilDestroyed(this.destroy)).subscribe(id=>{void this.load(id);});}
 async load(id:string):Promise<void>{this.abort.abort();const abort=new AbortController();this.abort=abort;this.booking=id;this.request++;this.view.set(null);this.statuses.set([]);this.error.set(null);this.loading.set(true);
 try{await this.session.ensureLoaded();const rows=await this.api.withAuthorizedClient(c=>c.intake.staffStatuses(id,abort.signal));if(!abort.signal.aborted)this.statuses.set(rows);}
 catch(e){if(!abort.signal.aborted)this.error.set(this.apiErr(e,'intake.loadError'));}finally{if(this.abort===abort)this.loading.set(false);}}
 async review(id:string):Promise<void>{const request=++this.request;const abort=this.abort;this.view.set(null);this.error.set(null);
 try{const view=await this.api.withAuthorizedClient(c=>c.intake.review(this.booking,id,abort.signal));if(!abort.signal.aborted&&request===this.request)this.view.set(view);}
 catch(e){if(!abort.signal.aborted&&request===this.request)this.error.set(this.apiErr(e,'intake.loadError'));}}
}
