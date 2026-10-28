import { describe,it,expect } from 'vitest';
import { intakeDefinitionSchema,intakeAnswerSchema,intakeStatusSchema } from './intake';
import { groupIntakeFields } from './intake-layout';
const id='11111111-1111-4111-8111-111111111111';
const sectionId='22222222-2222-4222-8222-222222222222';
describe('Intake contracts',()=>{
 it('accepts ordered non-clinical fields and preserves exact acknowledgement wording',()=>{
  const form=intakeDefinitionSchema.parse({title:'Consultation',description:null,instructions:null,fields:[{id,type:6,label:'Exact wording — Àda',helpText:null,required:true,choices:[]}]});
  expect(form.fields[0].label).toBe('Exact wording — Àda');
  expect(intakeDefinitionSchema.safeParse({...form,fields:[{...form.fields[0],type:7}]}).success).toBe(false);
  expect(intakeDefinitionSchema.safeParse({...form,script:'untrusted'}).success).toBe(false);
 });
 it('preserves false and rejects untyped malformed answers',()=>{
  expect(intakeAnswerSchema.parse({fieldId:id,boolean:false}).boolean).toBe(false);
  expect(intakeAnswerSchema.safeParse({fieldId:id,boolean:'false'}).success).toBe(false);
  expect(intakeAnswerSchema.safeParse({fieldId:id,fileUpload:'url'}).success).toBe(false);
  expect(intakeAnswerSchema.safeParse({fieldId:id,date:'25/10/2026'}).success).toBe(false);
 });
 it('accepts null sections from API when no sections exist',()=>{
  expect(intakeDefinitionSchema.safeParse({title:'Consultation',description:null,instructions:null,sections:null,fields:[{id,type:0,label:'Name',helpText:null,required:true,choices:[]}]}).success).toBe(true);
 });
 it('accepts optional sections and groups fields for display',()=>{
  const form=intakeDefinitionSchema.parse({title:'Consultation',description:null,instructions:null,sections:[{id:sectionId,title:'Personal information',description:null,sortOrder:0}],fields:[{id,type:0,label:'Name',helpText:null,required:true,choices:[],sectionId}]});
  expect(form.sections?.[0].title).toBe('Personal information');
  expect(groupIntakeFields(form)).toEqual([{kind:'section',section:form.sections![0],fields:form.fields}]);
  expect(intakeDefinitionSchema.safeParse({...form,fields:[{...form.fields[0],sectionId:'33333333-3333-4333-8333-333333333333'}]}).success).toBe(true);
 });
 it('distinguishes purged, pending and closed states without answer payloads',()=>{
  expect(intakeStatusSchema.parse({id,versionId:id,state:'purged',submittedAt:'2026-01-01',purgedAt:'2028-01-01',canSubmit:false}).state).toBe('purged');
  expect(intakeStatusSchema.safeParse({id,versionId:id,state:'unknown',submittedAt:null,purgedAt:null,canSubmit:false}).success).toBe(false);
 });
});
