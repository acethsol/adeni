import { Component, DestroyRef, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { IntakeFieldsComponent, PortalPageComponent, injectAdeniApiError, injectAdeniTranslate } from "@adeni/ui";
import type { IntakeDefinition, IntakeField, IntakeSummary, IntakeTemplate, ServiceOffering } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({selector:'app-intake-manager',standalone:true,imports:[FormsModule,RouterLink,PortalPageComponent,IntakeFieldsComponent],
 styles:`fieldset { margin-block:1rem; } label { display:block; margin-block:.5rem; } input,textarea,select { max-width:100%; padding:.5rem; } button { margin:.25rem; padding:.5rem; }`,
 templateUrl:'./intake-manager.component.html'})
export class IntakeManagerComponent implements OnInit {
 readonly tr=injectAdeniTranslate(); readonly apiErr=injectAdeniApiError(); private readonly api=inject(BusinessApiService); private readonly destroy=inject(DestroyRef);
 readonly rows=signal<IntakeSummary[]>([]); readonly selected=signal<IntakeTemplate|null>(null); readonly draft=signal<IntakeDefinition|null>(null);
 readonly services=signal<ServiceOffering[]>([]); readonly serviceId=signal(''); readonly binding=signal<{templateId:string|null;revision:string}|null>(null);
 readonly error=signal<string|null>(null); readonly busy=signal(false); readonly page=signal(1); readonly name=signal('');
 private abort=new AbortController(); private alive=true; private managementName="";
 constructor(){this.destroy.onDestroy(()=>{this.alive=false;this.abort.abort();this.draft.set(null);});}
 ngOnInit():void{void this.load();}
 async load():Promise<void>{await this.run(async()=>{
   const result=await this.api.withAuthorizedClient(async c=>({rows:await c.intake.listTemplates(this.page(),this.abort.signal),services:await c.getTenantServices()}));
   if(this.alive){this.rows.set(result.rows);this.services.set(result.services);}
 });}
 async run(fn:()=>Promise<void>):Promise<void>{if(this.busy())return;this.busy.set(true);this.error.set(null);try{await fn();}catch(e){if(this.alive&&!this.abort.signal.aborted)this.error.set(this.apiErr(e,'intake.loadError'));}finally{if(this.alive)this.busy.set(false);}}
 private accept(value:IntakeTemplate):void{if(!this.alive)return;this.selected.set(value);this.draft.set(value.draft ? structuredClone(value.draft):null);}
 async open(id:string):Promise<void>{this.managementName=this.rows().find(x=>x.id===id)?.name??"";await this.run(async()=>{this.accept(await this.api.withAuthorizedClient(c=>c.intake.getTemplate(id,this.abort.signal)));});}
 async create():Promise<void>{this.managementName=this.name();await this.run(async()=>{this.accept(await this.api.withAuthorizedClient(c=>c.intake.createTemplate(this.name())));this.name.set('');this.rows.set(await this.api.withAuthorizedClient(c=>c.intake.listTemplates(this.page(),this.abort.signal)));});}
 async action(kind:'save'|'publish'|'clone'|'active'):Promise<void>{const selected=this.selected();if(!selected)return;await this.run(async()=>{
   const value=await this.api.withAuthorizedClient(async c=>kind==='save'?c.intake.saveDraft(selected.id,selected.revision,this.draft()!):kind==='publish'?c.intake.publish(selected.id,(await c.intake.saveDraft(selected.id,selected.revision,this.draft()!)).revision):kind==='clone'?c.intake.cloneDraft(selected.id,selected.revision):c.intake.updateTemplate(selected.id,this.managementName,!selected.isActive,selected.revision));
   this.accept(value);if(this.alive)this.rows.set(await this.api.withAuthorizedClient(c=>c.intake.listTemplates(this.page(),this.abort.signal)));
 });}
 change(key:'title'|'description'|'instructions',value:string):void{this.draft.update(d=>d?{...d,[key]:value}:d);}
 field(index:number,key:'label'|'helpText'|'required'|'type',value:string|boolean|number):void{this.draft.update(d=>d?{...d,fields:d.fields.map((f,i)=>i===index?{...f,[key]:value,...(key==='type'?{choices:[]}: {})}:f)}:d);}
 add():void{this.draft.update(d=>d&&d.fields.length<30?{...d,fields:[...d.fields,{id:crypto.randomUUID(),type:0,label:'',helpText:null,required:false,choices:[]}]}:d);}
 remove(index:number):void{this.draft.update(d=>d?{...d,fields:d.fields.filter((_,i)=>i!==index)}:d);}
 move(index:number,delta:number):void{this.draft.update(d=>{if(!d||index+delta<0||index+delta>=d.fields.length)return d;const fields=[...d.fields];[fields[index],fields[index+delta]]=[fields[index+delta]!,fields[index]!];return {...d,fields};});}
 addChoice(index:number):void{this.draft.update(d=>d?{...d,fields:d.fields.map((f,i)=>i===index&&f.choices.length<20?{...f,choices:[...f.choices,{id:crypto.randomUUID(),label:''}]}:f)}:d);}
 choice(index:number,choice:number,label:string):void{this.draft.update(d=>d?{...d,fields:d.fields.map((f,i)=>i===index?{...f,choices:f.choices.map((c,j)=>j===choice?{...c,label}:c)}:f)}:d);}
 removeChoice(index:number,choice:number):void{this.draft.update(d=>d?{...d,fields:d.fields.map((f,i)=>i===index?{...f,choices:f.choices.filter((_,j)=>j!==choice)}:f)}:d);}
 async selectService(id:string):Promise<void>{this.serviceId.set(id);this.binding.set(null);if(!id)return;await this.run(async()=>{const value=await this.api.withAuthorizedClient(c=>c.intake.getBinding(id,this.abort.signal));if(this.alive)this.binding.set(value);});}
 async bind(templateId:string):Promise<void>{const binding=this.binding();if(!binding)return;await this.run(async()=>{const value=await this.api.withAuthorizedClient(c=>c.intake.setBinding(this.serviceId(),templateId||null,binding.revision));if(this.alive)this.binding.set(value);});}
 async paginate(delta:number):Promise<void>{if(this.busy())return;this.page.update(p=>Math.max(1,p+delta));await this.load();}
}
