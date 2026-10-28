import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  { path: "pay", renderMode: RenderMode.Client, headers: { "Cache-Control": "no-store, private", "X-Robots-Tag": "noindex, nofollow", "Referrer-Policy": "no-referrer" } },
  { path: "my-packages", renderMode: RenderMode.Client, headers: { "Cache-Control": "no-store, private", "X-Robots-Tag": "noindex, nofollow", "Referrer-Policy": "no-referrer" } },
  { path: "my-gift-cards", renderMode: RenderMode.Client, headers: { "Cache-Control": "no-store, private", "X-Robots-Tag": "noindex, nofollow", "Referrer-Policy": "no-referrer" } },
  { path: "my-bookings/:bookingId/intake/**", renderMode: RenderMode.Client, headers: { "Cache-Control": "no-store", "X-Robots-Tag": "noindex, nofollow" } },
  {
    path: "businesses/**",
    renderMode: RenderMode.Server,
  },
  {
    path: "**",
    renderMode: RenderMode.Server,
  },
];
