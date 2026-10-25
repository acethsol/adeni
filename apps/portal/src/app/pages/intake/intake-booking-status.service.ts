import { Injectable, inject, signal, type OnDestroy } from '@angular/core';
import type { IntakeStatus } from '@adeni/shared';
import { BusinessApiService } from '../../core/services/business-api.service';
@Injectable()
export class IntakeBookingStatusService implements OnDestroy {
 private readonly api=inject(BusinessApiService);private abort:AbortController|null=null;
 readonly rows=signal<Record<string,IntakeStatus[]>>({});readonly failed=signal(false);
 async load(ids:readonly string[]):Promise<void>{
  this.abort?.abort();const abort=new AbortController();this.abort=abort;this.rows.set({});this.failed.set(false);
  const unique=[...new Set(ids)];if(!unique.length)return;
  try{const result:Record<string,IntakeStatus[]>={};
   for(let i=0;i<unique.length;i+=50){if(abort.signal.aborted)return;Object.assign(result,await this.api.withAuthorizedClient(c=>c.intake.statusBatch(unique.slice(i,i+50),abort.signal)));}
   if(!abort.signal.aborted)this.rows.set(result);
  }catch{if(!abort.signal.aborted)this.failed.set(true);}
 }
 ngOnDestroy():void{this.abort?.abort();this.rows.set({});}
}
