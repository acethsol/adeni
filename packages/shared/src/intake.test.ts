import { describe,it,expect } from 'vitest';
import { intakeDefinitionSchema,intakeAnswerSchema,intakeStatusSchema } from './intake';
const id='11111111-1111-4111-8111-111111111111';
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
 it('distinguishes purged, pending and closed states without answer payloads',()=>{
  expect(intakeStatusSchema.parse({id,versionId:id,state:'purged',submittedAt:'2026-01-01',purgedAt:'2028-01-01',canSubmit:false}).state).toBe('purged');
  expect(intakeStatusSchema.safeParse({id,versionId:id,state:'unknown',submittedAt:null,purgedAt:null,canSubmit:false}).success).toBe(false);
 });
});
