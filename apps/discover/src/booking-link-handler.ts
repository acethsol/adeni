import type { Request, Response } from "express";
import { bookingLinkDestinationSchema, parseBookingPreselection, bookingPreselectionQuery } from "@adeni/shared/calendar";

/** Same-origin utility redirect; never render a second booking app or trust an API-provided absolute redirect. */
export function bookingLinkHandler(apiBase: string, request: typeof fetch = fetch) {
  return async (req: Request, res: Response): Promise<void> => {
    res.set({ "Cache-Control": "no-store", "X-Robots-Tag": "noindex, nofollow", "Referrer-Policy": "no-referrer" });
    const slug = req.params["slug"];
    if (typeof slug !== "string" || !/^[a-z0-9-]{1,64}$/.test(slug)) { res.status(404).send("Booking link unavailable"); return; }
    let query: string;
    try { query = bookingPreselectionQuery(parseBookingPreselection(new URL(req.originalUrl, "http://local.invalid").searchParams)); }
    catch { res.status(400).send("Invalid booking selection"); return; }
    try {
      const response = await request(`${apiBase}/api/v1/public/booking-links/${encodeURIComponent(slug)}${query}`, { redirect: "error", cache: "no-store", signal: AbortSignal.timeout(5000), headers: { Accept: "application/json" } });
      if (!response.ok) { res.status(response.status === 404 ? 404 : response.status === 400 ? 400 : 503).send("Booking link unavailable"); return; }
      const body = bookingLinkDestinationSchema.parse(await response.json());
      const destination = new URL(body.path, "https://local.invalid");
      if (destination.origin !== "https://local.invalid") throw new Error("Invalid destination");
      parseBookingPreselection(destination.searchParams);
      res.redirect(302, destination.pathname + destination.search + "#book");
    } catch { res.status(503).send("Booking link temporarily unavailable"); }
  };
}
