import { AdeniApiClient } from '@adeni/api-client';
import { intakeStatusSchema } from '@adeni/shared';
const id='11111111-1111-4111-8111-111111111111';
describe('Intake API contracts',()=>{
 it('retains the facade, authentication, no-store and idempotency headers',async()=>{
  const response={id,versionId:id,state:'completed',submittedAt:'2026-10-25',purgedAt:null,canSubmit:false};
  const fetcher=jasmine.createSpy().and.callFake(async()=>Response.json(response));
  const client=new AdeniApiClient({baseUrl:'https://api.example',accessToken:'jwt',fetchImpl:fetcher});
  const abort=new AbortController();
  expect((await client.intake.submit('b/b','a/a',id,[{fieldId:id,boolean:false}],'retry',abort.signal)).state).toBe('completed');
  const [url,init]=fetcher.calls.mostRecent().args;
  expect(url).toContain('/bookings/b%2Fb/intake/a%2Fa/submission');
  expect(new Headers(init.headers).get('Idempotency-Key')).toBe('retry');
  expect(new Headers(init.headers).get('Authorization')).toBe('Bearer jwt');
  expect(init.cache).toBe('no-store');expect(init.signal).toBe(abort.signal);
  expect(JSON.parse(init.body).answers[0].boolean).toBeFalse();
 });
 it('rejects malformed response bodies without weakening existing imports',async()=>{
  const api=new AdeniApiClient({baseUrl:'',fetchImpl:async()=>Response.json({answers:'invalid'})});
  await expectAsync(api.intake.customerRead('b','a')).toBeRejected();
  expect(intakeStatusSchema.safeParse({state:'clinical'}).success).toBeFalse();
 });
});
