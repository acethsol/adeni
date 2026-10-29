import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdeniLocaleService, IntakeFieldsComponent } from '@adeni/ui';
import type { AdeniApiClient } from '@adeni/api-client';
import type { IntakeTemplate,IntakeDefinition } from '@adeni/shared';
import { BusinessApiService } from '../../core/services/business-api.service';
import { IntakeManagerComponent } from './intake-manager.component';
const id='11111111-1111-4111-8111-111111111111';const second='22222222-2222-4222-8222-222222222222';
const definition:IntakeDefinition={title:'Consultation',description:null,instructions:null,fields:[{id,type:0,label:'Preference',helpText:null,required:false,choices:[]}]};
const template:IntakeTemplate={id,isActive:true,revision:'revision',publishedVersionId:null,draftVersionId:id,draft:definition,published:null};
describe('Intake manager',()=>{
 let api:{listTemplates:jasmine.Spy;getTemplate:jasmine.Spy;saveDraft:jasmine.Spy;publish:jasmine.Spy;updateTemplate:jasmine.Spy;getBinding:jasmine.Spy;setBinding:jasmine.Spy};
 beforeEach(()=>{
  api={listTemplates:jasmine.createSpy().and.resolveTo([{id,name:'Consultation',isActive:true,publishedVersionId:id,revision:'revision'}]),getTemplate:jasmine.createSpy().and.resolveTo(template),saveDraft:jasmine.createSpy().and.resolveTo({...template,revision:'saved'}),publish:jasmine.createSpy().and.resolveTo({...template,draft:null,published:definition,publishedVersionId:id}),updateTemplate:jasmine.createSpy().and.resolveTo({...template,isActive:false,revision:'inactive'}),getBinding:jasmine.createSpy().and.resolveTo({templateId:null,revision:''}),setBinding:jasmine.createSpy().and.resolveTo({templateId:id,revision:'bound'})};
  const client={intake:api,getTenantServices:async()=>[{id:second,name:'Hair care'}]} as unknown as AdeniApiClient;
  TestBed.configureTestingModule({imports:[IntakeManagerComponent],providers:[provideRouter([]),{provide:AdeniLocaleService,useValue:{locale:signal('en')}},{provide:BusinessApiService,useValue:{withAuthorizedClient:(fn:(c:AdeniApiClient)=>unknown)=>fn(client)}}]});
 });
 it('edits ordered fields without changing the published snapshot and saves before publishing',async()=>{
  const fixture=TestBed.createComponent(IntakeManagerComponent);fixture.detectChanges();await fixture.whenStable();
  const component=fixture.componentInstance;await component.open(id);component.add();component.move(1,-1);
  expect(component.draft()!.fields[1].id).toBe(id);component.change('title','Changed title');
  await component.action('publish');
  expect(api.saveDraft).toHaveBeenCalledWith(id,'revision',jasmine.objectContaining({title:'Changed title'}));
  expect(api.publish).toHaveBeenCalledWith(id,'saved');
  expect(definition.title).toBe('Consultation');expect(component.draft()).toBeNull();
 });
 it('deactivates from the grid and refreshes list rows',async()=>{
  const activeRow={id,name:'Consultation',isActive:true,publishedVersionId:id,revision:'revision'};
  const inactiveRow={...activeRow,isActive:false,revision:'inactive'};
  api.listTemplates.and.returnValues(Promise.resolve([activeRow]),Promise.resolve([inactiveRow]));
  const fixture=TestBed.createComponent(IntakeManagerComponent);fixture.detectChanges();await fixture.whenStable();
  const component=fixture.componentInstance;
  await component.toggleFormActive(activeRow);
  expect(api.updateTemplate).toHaveBeenCalledWith(id,'Consultation',false,'revision');
  expect(api.listTemplates).toHaveBeenCalledTimes(2);
  expect(component.rows()[0].isActive).toBeFalse();
 });
 it('refreshes the forms grid after publish',async()=>{
  const publishedRow={id,name:'Consultation',isActive:true,publishedVersionId:id,revision:'revision'};
  api.listTemplates.and.returnValues(
    Promise.resolve([{...publishedRow,publishedVersionId:null}]),
    Promise.resolve([publishedRow]),
  );
  const fixture=TestBed.createComponent(IntakeManagerComponent);fixture.detectChanges();await fixture.whenStable();
  const component=fixture.componentInstance;expect(component.rows()[0].publishedVersionId).toBeNull();
  await component.open(id);await component.action('publish');
  expect(api.listTemplates).toHaveBeenCalledTimes(2);
  expect(component.rows()[0].publishedVersionId).toBe(id);
 });
 it('loads service scope and saves exactly one required template binding',async()=>{
  const fixture=TestBed.createComponent(IntakeManagerComponent);fixture.detectChanges();await fixture.whenStable();const component=fixture.componentInstance;
  await component.selectService(second);await component.bind(id);
  expect(api.getBinding).toHaveBeenCalledWith(second,jasmine.any(AbortSignal));expect(api.setBinding).toHaveBeenCalledWith(second,id,'');expect(component.binding()?.templateId).toBe(id);
 });
 it('aborts pending reads on destroy and discards late definitions',async()=>{
  const fixture=TestBed.createComponent(IntakeManagerComponent);fixture.detectChanges();await fixture.whenStable();
  let finish!:(value:IntakeTemplate)=>void;api.getTemplate.and.returnValue(new Promise(resolve=>{finish=resolve;}));
  const pending=fixture.componentInstance.open(id);const abort=api.getTemplate.calls.mostRecent().args[1] as AbortSignal;fixture.destroy();finish(template);await pending;
  expect(abort.aborted).toBeTrue();expect(fixture.componentInstance.draft()).toBeNull();
 });
});
describe('Intake field presentation',()=>{
 beforeEach(()=>TestBed.configureTestingModule({imports:[IntakeFieldsComponent],providers:[{provide:AdeniLocaleService,useValue:{locale:signal('en')}}]}));
 it('emits explicit false for yes/no rather than treating it as a missing answer',()=>{
  const fixture=TestBed.createComponent(IntakeFieldsComponent);fixture.componentRef.setInput('definition',{...definition,fields:[{...definition.fields[0],type:4,required:true}]});fixture.detectChanges();
  const changed=jasmine.createSpy();fixture.componentInstance.answersChange.subscribe(changed);const select=fixture.nativeElement.querySelector('select') as HTMLSelectElement;select.value='false';select.dispatchEvent(new Event('change'));
  expect(changed).toHaveBeenCalledWith([{fieldId:id,boolean:false}]);
 });
 it('renders response text as text in read-only review',()=>{
  const fixture=TestBed.createComponent(IntakeFieldsComponent);fixture.componentRef.setInput('definition',definition);fixture.componentRef.setInput('readOnly',true);fixture.componentRef.setInput('answers',[{fieldId:id,text:'<script>sensitive-sentinel</script>'}]);fixture.detectChanges();
  expect(fixture.nativeElement.querySelector('script')).toBeNull();expect(fixture.nativeElement.textContent).toContain('<script>sensitive-sentinel</script>');expect(fixture.nativeElement.querySelector('input')).toBeNull();
 });
 it('renders disabled controls in preview mode without showing unanswered copy',()=>{
  const fixture=TestBed.createComponent(IntakeFieldsComponent);fixture.componentRef.setInput('definition',definition);fixture.componentRef.setInput('preview',true);fixture.detectChanges();
  const input=fixture.nativeElement.querySelector('input[type=text]') as HTMLInputElement;const label=fixture.nativeElement.querySelector('.field label') as HTMLLabelElement;
  expect(input.disabled).toBeTrue();expect(input).not.toBeNull();expect(input.value).toBe('');expect(label.textContent).toContain('Preference');expect(fixture.nativeElement.textContent).not.toContain('Not answered');
 });
 it('shows section headings when the definition includes sections',()=>{
  const sectionId='22222222-2222-4222-8222-222222222222';
  const withSection={...definition,sections:[{id:sectionId,title:'Personal information',description:'Guest details',sortOrder:0}],fields:[{...definition.fields[0],sectionId}]};
  const fixture=TestBed.createComponent(IntakeFieldsComponent);fixture.componentRef.setInput('definition',withSection);fixture.componentRef.setInput('preview',true);fixture.detectChanges();
  expect(fixture.nativeElement.querySelector('.intake-section__title')?.textContent).toContain('Personal information');
  expect(fixture.nativeElement.textContent).toContain('Guest details');
 });
});
