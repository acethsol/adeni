import { vi } from "vitest";
import { bookingLinkHandler } from "./booking-link-handler";
import type { Request,Response } from "express";
const id="11111111-1111-4111-8111-111111111111";
function response(){return {set:vi.fn(),status:vi.fn().mockReturnThis(),send:vi.fn(),redirect:vi.fn()} as unknown as Response;}
describe("Book-with-Adeni server redirect",()=>{
  it("redirects to the existing business route with private utility headers",async()=>{
    const fetcher=vi.fn().mockResolvedValue(new globalThis.Response(JSON.stringify({path:`/businesses/current-slug?service=${id}#book`,serviceId:id,practitionerId:null})));
    const res=response();await bookingLinkHandler("https://api.example.test",fetcher)({params:{slug:"stable-slug"},originalUrl:`/book/stable-slug?service=${id}`} as unknown as Request,res);
    expect(res.redirect).toHaveBeenCalledWith(302,`/businesses/current-slug?service=${id}#book`);expect(res.set).toHaveBeenCalledWith(expect.objectContaining({"X-Robots-Tag":"noindex, nofollow","Cache-Control":"no-store"}));
  });
  it("rejects unsafe selectors before calling the API",async()=>{
    const fetcher=vi.fn();const res=response();await bookingLinkHandler("https://api.example.test",fetcher)({params:{slug:"studio"},originalUrl:"/book/studio?price=1"} as unknown as Request,res);
    expect(res.status).toHaveBeenCalledWith(400);expect(fetcher).not.toHaveBeenCalled();
  });
  it("does not follow an API-provided external redirect",async()=>{
    const fetcher=vi.fn().mockResolvedValue(new globalThis.Response(JSON.stringify({path:"https://evil.example.test",serviceId:null,practitionerId:null})));
    const res=response();await bookingLinkHandler("https://api.example.test",fetcher)({params:{slug:"studio"},originalUrl:"/book/studio"} as unknown as Request,res);
    expect(res.status).toHaveBeenCalledWith(503);expect(res.redirect).not.toHaveBeenCalled();
  });
});
