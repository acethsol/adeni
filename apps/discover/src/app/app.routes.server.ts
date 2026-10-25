import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
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
