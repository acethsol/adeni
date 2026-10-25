import { PLATFORM_ID, signal, TransferState } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AdeniLocaleService } from '@adeni/ui';
import { RenderMode } from '@angular/ssr';
import { IntakeComponent } from './intake.component';
import { CustomerApiService } from '../../core/services/customer-api.service';
import { serverRoutes } from '../../app.routes.server';

describe('private Intake rendering',()=>{
 it('uses client rendering and no-store/noindex only for the private intake route',()=>{
  const route=serverRoutes.find(r=>r.path==='my-bookings/:bookingId/intake/**')!;
  expect(route.renderMode).toBe(RenderMode.Client);
  expect(route.headers?.['Cache-Control']).toBe('no-store');
  expect(route.headers?.['X-Robots-Tag']).toContain('noindex');
  expect(serverRoutes.find(r=>r.path==='businesses/**')!.renderMode).toBe(RenderMode.Server);
  expect(serverRoutes.find(r=>r.path==='**')!.renderMode).toBe(RenderMode.Server);
 });
 it('does not fetch private data or populate TransferState during server rendering',async()=>{
  const api={withAuthorizedClient:vi.fn()};
  TestBed.configureTestingModule({imports:[IntakeComponent],providers:[provideRouter([]),
   {provide:PLATFORM_ID,useValue:'server'},{provide:CustomerApiService,useValue:api},
   {provide:AdeniLocaleService,useValue:{locale:signal('en')}},
   {provide:ActivatedRoute,useValue:{paramMap:of(convertToParamMap({bookingId:'private-id',assignmentId:'private-form'}))}}]});
  const fixture=TestBed.createComponent(IntakeComponent);fixture.detectChanges();await fixture.whenStable();
  expect(api.withAuthorizedClient).not.toHaveBeenCalled();
  expect(TestBed.inject(TransferState).toJson()).toBe('{}');
  expect(fixture.nativeElement.textContent).not.toContain('private-id');
  expect(fixture.nativeElement.querySelector('input')).toBeNull();
 });
});
