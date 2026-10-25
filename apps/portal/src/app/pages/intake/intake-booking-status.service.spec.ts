import { TestBed } from '@angular/core/testing';
import type { AdeniApiClient } from '@adeni/api-client';
import type { IntakeStatus } from '@adeni/shared';
import { BusinessApiService } from '../../core/services/business-api.service';
import { IntakeBookingStatusService } from './intake-booking-status.service';
describe('Intake booking status batches',()=>{
 let batch:jasmine.Spy;
 beforeEach(()=>{batch=jasmine.createSpy().and.resolveTo({});const client={intake:{statusBatch:batch}} as unknown as AdeniApiClient;
 TestBed.configureTestingModule({providers:[IntakeBookingStatusService,{provide:BusinessApiService,useValue:{withAuthorizedClient:(fn:(api:AdeniApiClient)=>unknown)=>fn(client)}}]});});
 it('bounds calls to 50 authorized booking identifiers per batch',async()=>{
  const ids=Array.from({length:101},(_,i)=>String(i));await TestBed.inject(IntakeBookingStatusService).load(ids);
  expect(batch.calls.count()).toBe(3);expect(batch.calls.allArgs().map(args=>args[0].length)).toEqual([50,50,1]);
 });
 it('aborts obsolete scope requests and ignores late metadata',async()=>{
  let finish!:(value:Record<string,IntakeStatus[]>)=>void;batch.and.returnValue(new Promise(resolve=>{finish=resolve;}));
  const service=TestBed.inject(IntakeBookingStatusService);const old=service.load(['old']);const signal=batch.calls.mostRecent().args[1] as AbortSignal;
  batch.and.resolveTo({new:[]});await service.load(['new']);finish({old:[]});await old;
  expect(signal.aborted).toBeTrue();expect(service.rows()).toEqual({new:[]});service.ngOnDestroy();expect(service.rows()).toEqual({});
 });
});
