import { describe, expect, it } from "vitest";
import { parseBookingPreselection, bookingPreselectionQuery, calendarConnectionSchema, bookingLinkDestinationSchema } from "./calendar";
import { AdeniApiClient } from "../../api-client/src/index";
const id="11111111-1111-4111-8111-111111111111";
describe("calendar and booking-link contracts",()=>{
  it("permits only bounded public selectors",()=>{
    expect(parseBookingPreselection(new URLSearchParams(`service=${id}&practitioner=${id}`))).toEqual({service:id,practitioner:id});
    for(const raw of ["price=1","tenant=private","currency=CAD","service=invalid",`service=${id}&service=${id}`,"returnUrl=https://evil.test","service=00000000-0000-0000-0000-000000000000"])
      expect(()=>parseBookingPreselection(new URLSearchParams(raw))).toThrow();
    expect(bookingPreselectionQuery({service:id})).toBe(`?service=${id}`);
  });
  it("strips credentials from public management DTOs and rejects external redirects",()=>{
    const parsed=calendarConnectionSchema.parse({id,staffMemberId:id,provider:"google",status:"connected",lastSyncedAt:null,coveredUntil:null,failureCategory:null,selections:[],accessToken:"secret",protectedCredentials:"secret"});
    expect(parsed).not.toHaveProperty("accessToken");expect(parsed).not.toHaveProperty("protectedCredentials");
    for(const path of ["//evil.test","https://evil.test","/pay#t=secret","/businesses/slug\r\nLocation:evil"])
      expect(bookingLinkDestinationSchema.safeParse({path,serviceId:null,practitionerId:null}).success).toBe(false);
  });
  it("preserves the api-client facade, normal transport and response validation",async()=>{
    const calls:{url:string;init:RequestInit}[]=[];
    const fetchFn:typeof fetch=async(input,init)=>{calls.push({url:String(input),init:init??{}});return new Response(JSON.stringify({path:`/businesses/studio?service=${id}#book`,serviceId:id,practitionerId:null}),{status:200,headers:{"Content-Type":"application/json"}});};
    const client=new AdeniApiClient({baseUrl:"https://api.example.test",fetchImpl:fetchFn});
    const result=await client.calendar.resolveLink("studio",{service:id});expect(result.serviceId).toBe(id);expect(calls[0].url).toBe(`https://api.example.test/api/v1/public/booking-links/studio?service=${id}`);expect(calls[0].init.cache).toBe("no-store");
  });
});
